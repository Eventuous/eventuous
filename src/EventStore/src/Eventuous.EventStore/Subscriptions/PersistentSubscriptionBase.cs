// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Eventuous.Tools;

// ReSharper disable SuggestBaseTypeForParameter

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Function type for handling event processing failures
/// </summary>
public delegate Task HandleEventProcessingFailure(
        EventStoreClient       client,
        PersistentSubscription subscription,
        ResolvedEvent          resolvedEvent,
        Exception              exception
    );

/// <summary>
/// Base class for EventStoreDB persistent subscriptions
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class PersistentSubscriptionBase<T> : EventSubscription<T> where T : PersistentSubscriptionOptions {
    /// <summary>
    /// EventStoreDB persistent subscription client instance.
    /// </summary>
    protected EventStorePersistentSubscriptionsClient SubscriptionClient { get; }

    /// <summary>
    /// EventStoreDB client instance. It's used for custom NACK behavior as well as for measuring the subscription gap. 
    /// </summary>
    protected EventStoreClient EventStoreClient { get; }

    /// <summary>
    /// Metadata serializer instance.
    /// </summary>
    protected IMetadataSerializer MetadataSerializer { get; }

    readonly HandleEventProcessingFailure _handleEventProcessingFailure;

    PersistentSubscription? _subscription;

    /// <summary>
    /// EventStoreDB persistent subscription base class constructor
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB client instance</param>
    /// <param name="options">Subscription options</param>
    /// <param name="consumePipe">Consume pipe instance, provided automatically</param>
    /// <param name="loggerFactory">Optional logger factory</param>
    /// <param name="eventSerializer">Event payload serializer</param>
    /// <param name="metaSerializer">Metadata serializer</param>
    protected PersistentSubscriptionBase(
            EventStoreClient     eventStoreClient,
            T                    options,
            ConsumePipe          consumePipe,
            ILoggerFactory?      loggerFactory,
            IEventSerializer?    eventSerializer,
            IMetadataSerializer? metaSerializer
        )
        : base(options, consumePipe, loggerFactory, eventSerializer) {
        EventStoreClient   = eventStoreClient;
        MetadataSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
        var settings   = eventStoreClient.GetSettings().Copy();
        var opSettings = settings.OperationOptions.Clone();
        settings.OperationOptions     = opSettings;
        SubscriptionClient            = new(settings);
        _handleEventProcessingFailure = options.FailureHandler ?? DefaultEventProcessingFailureHandler;
        if (options is { FailureHandler: not null, ThrowOnError: false }) Log.ThrowOnErrorIncompatible();
    }

    /// <summary>
    /// EventStoreDB persistent subscription base class constructor
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB persistent subscription client instance</param>
    /// <param name="options">Subscription options</param>
    /// <param name="consumePipe">Consume pipe instance, provided automatically</param>
    /// <param name="loggerFactory">Optional logger factory</param>
    /// <param name="eventSerializer"></param>
    /// <param name="metaSerializer">Metadata serializer</param>
    protected PersistentSubscriptionBase(
            EventStorePersistentSubscriptionsClient eventStoreClient,
            T                                       options,
            ConsumePipe                             consumePipe,
            ILoggerFactory?                         loggerFactory,
            IEventSerializer?                       eventSerializer,
            IMetadataSerializer?                    metaSerializer
        )
        : base(options, consumePipe, loggerFactory, eventSerializer) {
        SubscriptionClient = eventStoreClient;
        MetadataSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
        var settings   = eventStoreClient.GetSettings().Copy();
        var opSettings = settings.OperationOptions.Clone();
        settings.OperationOptions     = opSettings;
        EventStoreClient              = new(settings);
        _handleEventProcessingFailure = options.FailureHandler ?? DefaultEventProcessingFailureHandler;
        if (options is { FailureHandler: not null, ThrowOnError: false }) Log.ThrowOnErrorIncompatible();
    }

    const string ResolvedEventKey = "resolvedEvent";
    const string SubscriptionKey  = "subscription";

    /// <summary>
    /// Execute an operation to set up a persistent subscription
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract Task CreatePersistentSubscription(PersistentSubscriptionSettings settings, CancellationToken cancellationToken);

    /// <summary>
    /// Subscribe to a persistent subscription
    /// </summary>
    /// <param name="cancellationToken"></param>
    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var settings = Options.SubscriptionSettings ?? new PersistentSubscriptionSettings(Options.ResolveLinkTos);

        try {
            _subscription = await LocalSubscribe(HandleEvent, HandleDrop, cancellationToken).NoContext();
        } catch (PersistentSubscriptionNotFoundException) {
            await CreatePersistentSubscription(settings, cancellationToken);

            _subscription = await LocalSubscribe(HandleEvent, HandleDrop, cancellationToken).NoContext();
        }

        return;

        void HandleDrop(PersistentSubscription __, SubscriptionDroppedReason reason, Exception? exception)
            => Dropped(EsdbMappings.AsDropReason(reason), exception);

        async Task HandleEvent(PersistentSubscription subscription, ResolvedEvent re, int? retryCount, CancellationToken ct) {
            Logger.Configure(Options.SubscriptionId, LoggerFactory);

            var context = CreateContext(re, ct)
                .WithItem(ResolvedEventKey, re)
                .WithItem(SubscriptionKey, subscription);

            try {
                await Handler(context).NoContext();
                LastProcessed = EventPosition.FromContext(context);
                await Ack(context).NoContext();
            } catch (OperationCanceledException e) when (ct.IsCancellationRequested) {
                Dropped(DropReason.Stopped, e);
            } catch (Exception e) {
                await Nack(context, e).NoContext();
            }
        }
    }

    /// <summary>
    /// Last processed event position
    /// </summary>
    protected EventPosition? LastProcessed { [PublicAPI] get; set; }

    /// <summary>
    /// Internal method to subscribe to a persistent subscription
    /// </summary>
    /// <param name="eventAppeared"></param>
    /// <param name="subscriptionDropped"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract Task<PersistentSubscription> LocalSubscribe(
            Func<PersistentSubscription, ResolvedEvent, int?, CancellationToken, Task> eventAppeared,
            Action<PersistentSubscription, SubscriptionDroppedReason, Exception?>?     subscriptionDropped,
            CancellationToken                                                          cancellationToken
        );

    ConcurrentQueue<ResolvedEvent> AckQueue { get; } = new();

    async ValueTask Ack(IMessageConsumeContext ctx) {
        var re           = ctx.Items.GetItem<ResolvedEvent>(ResolvedEventKey);
        var subscription = ctx.Items.GetItem<PersistentSubscription>(SubscriptionKey)!;
        await subscription.Ack(re).NoContext();
    }

    async ValueTask Nack(IMessageConsumeContext ctx, Exception exception) {
        if (exception is OperationCanceledException && ctx.CancellationToken.IsCancellationRequested) {
            return;
        }

        ctx.LogContext.MessageHandlingFailed(Options.SubscriptionId, ctx, exception);

        if (Options.ThrowOnError) throw exception;

        var re           = ctx.Items.GetItem<ResolvedEvent>(ResolvedEventKey);
        var subscription = ctx.Items.GetItem<PersistentSubscription>(SubscriptionKey)!;
        await _handleEventProcessingFailure(EventStoreClient, subscription, re, exception).NoContext();
    }

    MessageConsumeContext CreateContext(ResolvedEvent re, CancellationToken cancellationToken) {
        var evt = DeserializeData(
            re.Event.ContentType,
            re.Event.EventType,
            re.Event.Data,
            re.Event.EventStreamId,
            re.Event.Position.CommitPosition
        );

        return new(
            re.Event.EventId.ToString(),
            re.Event.EventType,
            re.Event.ContentType,
            re.Event.EventStreamId,
            re.Event.EventNumber,
            GetContextStreamPosition(re),
            re.Event.Position.CommitPosition,
            Sequence++,
            re.Event.Created,
            evt,
            MetadataSerializer.DeserializeMeta(Options, re.Event.Metadata, re.Event.EventStreamId, re.Event.EventNumber),
            SubscriptionId,
            cancellationToken
        );
    }

    /// <summary>
    /// Get stream position from the resolved event
    /// </summary>
    /// <param name="re">Resolved event received from the database</param>
    /// <returns></returns>
    protected abstract ulong GetContextStreamPosition(ResolvedEvent re);

    /// <summary>
    /// Unsubscribe from a persistent subscription
    /// </summary>
    /// <param name="cancellationToken"></param>
    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        try {
            _subscription?.Dispose();
            Stopping.Cancel(false);
            await Task.Delay(100, cancellationToken);
        } catch (Exception) {
            // It might throw
        }
    }

    static Task DefaultEventProcessingFailureHandler(
            EventStoreClient       client,
            PersistentSubscription subscription,
            ResolvedEvent          resolvedEvent,
            Exception              exception
        )
        => subscription.Nack(PersistentSubscriptionNakEventAction.Retry, exception.Message, resolvedEvent);
}
