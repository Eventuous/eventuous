// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Eventuous.Tools;

// ReSharper disable SuggestBaseTypeForParameter

namespace Eventuous.EventStore.Subscriptions;

public delegate Task HandleEventProcessingFailure(
    EventStoreClient       client,
    PersistentSubscription subscription,
    ResolvedEvent          resolvedEvent,
    Exception              exception
);

public abstract class PersistentSubscriptionBase<T> : EventSubscription<T>
    where T : PersistentSubscriptionOptions {
    protected EventStorePersistentSubscriptionsClient SubscriptionClient { get; }

    readonly HandleEventProcessingFailure _handleEventProcessingFailure;

    PersistentSubscription? _subscription;

    protected PersistentSubscriptionBase(
        EventStoreClient eventStoreClient,
        T                options,
        ConsumePipe      consumePipe,
        ILoggerFactory?  loggerFactory
    ) : base(options, consumePipe, loggerFactory) {
        EventStoreClient = eventStoreClient;
        var settings   = eventStoreClient.GetSettings().Copy();
        var opSettings = settings.OperationOptions.Clone();
        settings.OperationOptions = opSettings;

        SubscriptionClient = new EventStorePersistentSubscriptionsClient(settings);

        _handleEventProcessingFailure = options.FailureHandler ?? DefaultEventProcessingFailureHandler;

        if (options.FailureHandler != null && !options.ThrowOnError) Log.ThrowOnErrorIncompatible();
    }

    protected EventStoreClient EventStoreClient { get; }

    const string ResolvedEventKey = "resolvedEvent";
    const string SubscriptionKey  = "subscription";

    protected abstract Task CreatePersistentSubscription(
        PersistentSubscriptionSettings settings,
        CancellationToken              cancellationToken
    );

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var settings = Options.SubscriptionSettings ?? new PersistentSubscriptionSettings(Options.ResolveLinkTos);

        try {
            _subscription = await LocalSubscribe(
                    (subscription, @event, retryCount, ct) => HandleEvent(subscription, @event, retryCount, ct),
                    HandleDrop,
                    cancellationToken
                )
                .NoContext();
        }
        catch (PersistentSubscriptionNotFoundException) {
            await CreatePersistentSubscription(settings, cancellationToken);

            _subscription = await LocalSubscribe(
                    (subscription, @event, retryCount, ct) => HandleEvent(subscription, @event, retryCount, ct),
                    HandleDrop,
                    cancellationToken
                )
                .NoContext();
        }

        void HandleDrop(
            PersistentSubscription    __,
            SubscriptionDroppedReason reason,
            Exception?                exception
        )
            => Dropped(EsdbMappings.AsDropReason(reason), exception);

        async Task HandleEvent(
            PersistentSubscription subscription,
            ResolvedEvent          re,
            int?                   retryCount,
            CancellationToken      ct
        ) {
            Logger.Configure(Options.SubscriptionId, LoggerFactory);

            var context = CreateContext(re, ct)
                .WithItem(ResolvedEventKey, re)
                .WithItem(SubscriptionKey, subscription);

            try {
                await Handler(context).NoContext();
                LastProcessed = EventPosition.FromContext(context);
                await Ack(context).NoContext();
            }
            catch (Exception e) {
                await Nack(context, e).NoContext();
            }
        }
    }

    protected EventPosition? LastProcessed { get; set; }

    protected abstract Task<PersistentSubscription> LocalSubscribe(
        Func<PersistentSubscription, ResolvedEvent, int?, CancellationToken, Task> eventAppeared,
        Action<PersistentSubscription, SubscriptionDroppedReason, Exception?>?     subscriptionDropped,
        CancellationToken                                                          cancellationToken
    );

    ConcurrentQueue<ResolvedEvent> AckQueue { get; } = new();

    async ValueTask Ack(IMessageConsumeContext ctx) {
        var re = ctx.Items.GetItem<ResolvedEvent>(ResolvedEventKey);
        AckQueue.Enqueue(re);

        if (AckQueue.Count < Options.BufferSize) return;

        var subscription = ctx.Items.GetItem<PersistentSubscription>(SubscriptionKey)!;

        var toAck = new List<ResolvedEvent>();

        for (var i = 0; i < Options.BufferSize; i++) {
            if (AckQueue.TryDequeue(out var evt)) toAck.Add(evt);
        }

        await subscription.Ack(toAck).NoContext();
    }

    async ValueTask Nack(IMessageConsumeContext ctx, Exception exception) {
        ctx.LogContext.MessageHandlingFailed(Options.SubscriptionId, ctx, exception);

        if (Options.ThrowOnError) throw exception;

        var re           = ctx.Items.GetItem<ResolvedEvent>(ResolvedEventKey);
        var subscription = ctx.Items.GetItem<PersistentSubscription>(SubscriptionKey)!;
        await _handleEventProcessingFailure(EventStoreClient, subscription, re, exception).NoContext();
    }

    IMessageConsumeContext CreateContext(ResolvedEvent re, CancellationToken cancellationToken) {
        var evt = DeserializeData(
            re.Event.ContentType,
            re.Event.EventType,
            re.Event.Data,
            re.OriginalStreamId,
            re.Event.Position.CommitPosition
        );

        return new MessageConsumeContext(
            re.Event.EventId.ToString(),
            re.Event.EventType,
            re.Event.ContentType,
            re.OriginalStreamId,
            GetContextStreamPosition(re),
            re.Event.Position.CommitPosition,
            re.OriginalEventNumber,
            re.Event.Created,
            evt,
            Options.MetadataSerializer.DeserializeMeta(
                Options,
                re.Event.Metadata,
                re.OriginalStreamId,
                re.Event.EventNumber
            ),
            SubscriptionId,
            cancellationToken
        );
    }

    protected abstract ulong GetContextStreamPosition(ResolvedEvent re);

    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        try {
            Stopping.Cancel(false);
            await Task.Delay(100, cancellationToken);
            _subscription?.Dispose();
        }
        catch (Exception) {
            // It might throw
        }
    }

    static Task DefaultEventProcessingFailureHandler(
        EventStoreClient       client,
        PersistentSubscription subscription,
        ResolvedEvent          resolvedEvent,
        Exception              exception
    )
        => subscription.Nack(
            PersistentSubscriptionNakEventAction.Retry,
            exception.Message,
            resolvedEvent
        );
}
