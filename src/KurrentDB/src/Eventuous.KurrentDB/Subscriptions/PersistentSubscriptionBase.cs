// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Eventuous.Tools;

// ReSharper disable SuggestBaseTypeForParameter

namespace Eventuous.KurrentDB.Subscriptions;

/// <summary>
/// Function type for handling event processing failures
/// </summary>
public delegate Task HandleEventProcessingFailure(
        KurrentDBClient        client,
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
    protected KurrentDBPersistentSubscriptionsClient SubscriptionClient { get; }

    /// <summary>
    /// EventStoreDB client instance. It's used for custom NACK behavior as well as for measuring the subscription gap.
    /// </summary>
    protected KurrentDBClient Client { get; }

    /// <summary>
    /// Metadata serializer instance.
    /// </summary>
    protected IMetadataSerializer MetadataSerializer { get; }

    readonly HandleEventProcessingFailure _handleEventProcessingFailure;

    /// <summary>
    /// EventStoreDB persistent subscription base class constructor
    /// </summary>
    /// <param name="client">EventStoreDB client instance</param>
    /// <param name="options">Subscription options</param>
    /// <param name="consumePipe">Consume pipe instance, provided automatically</param>
    /// <param name="loggerFactory">Optional logger factory</param>
    /// <param name="eventSerializer">Event payload serializer</param>
    /// <param name="metaSerializer">Metadata serializer</param>
    protected PersistentSubscriptionBase(
            KurrentDBClient      client,
            T                    options,
            ConsumePipe          consumePipe,
            ILoggerFactory?      loggerFactory,
            IEventSerializer?    eventSerializer,
            IMetadataSerializer? metaSerializer
        )
        : base(options, consumePipe, loggerFactory, eventSerializer) {
        Client             = client;
        MetadataSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;

        var settings   = client.GetSettings().Copy();
        var opSettings = settings.OperationOptions.Clone();
        settings.OperationOptions     = opSettings;
        SubscriptionClient            = new(settings);
        _handleEventProcessingFailure = options.FailureHandler ?? DefaultEventProcessingFailureHandler;
        if (options is { FailureHandler: not null, ThrowOnError: false }) Log.ThrowOnErrorIncompatible();
    }

    /// <summary>
    /// EventStoreDB persistent subscription base class constructor
    /// </summary>
    /// <param name="client">EventStoreDB persistent subscription client instance</param>
    /// <param name="options">Subscription options</param>
    /// <param name="consumePipe">Consume pipe instance, provided automatically</param>
    /// <param name="loggerFactory">Optional logger factory</param>
    /// <param name="eventSerializer"></param>
    /// <param name="metaSerializer">Metadata serializer</param>
    protected PersistentSubscriptionBase(
            KurrentDBPersistentSubscriptionsClient client,
            T                                      options,
            ConsumePipe                            consumePipe,
            ILoggerFactory?                        loggerFactory,
            IEventSerializer?                      eventSerializer,
            IMetadataSerializer?                   metaSerializer
        )
        : base(options, consumePipe, loggerFactory, eventSerializer) {
        SubscriptionClient = client;
        MetadataSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
        var settings   = client.GetSettings().Copy();
        var opSettings = settings.OperationOptions.Clone();
        settings.OperationOptions     = opSettings;
        Client                        = new(settings);
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
    protected override async ValueTask Connect(SubscriptionRun run) {
        var settings = Options.SubscriptionSettings ?? new PersistentSubscriptionSettings(Options.ResolveLinkTos);

        PersistentSubscription connected;

        try {
            connected = await LocalSubscribe(HandleEvent, HandleDrop, run.Token).NoContext();
        } catch (PersistentSubscriptionNotFoundException) {
            await CreatePersistentSubscription(settings, run.Token);

            connected = await LocalSubscribe(HandleEvent, HandleDrop, run.Token).NoContext();
        }

        // No settling delay needed: the supervisor now ignores failures raised while shutting down.
        run.OnDisconnect(_ => { connected.Dispose(); return default; });

        return;

        void HandleDrop(PersistentSubscription __, SubscriptionDroppedReason reason, Exception? exception)
            => run.Fail(KurrentDBMappings.AsDropReason(reason), exception);

        async Task HandleEvent(PersistentSubscription subscription, ResolvedEvent re, int? retryCount, CancellationToken ct) {
            Logger.Configure(Options.SubscriptionId, LoggerFactory);

            var context = CreateContext(run, re, ct)
                .WithItem(ResolvedEventKey, re)
                .WithItem(SubscriptionKey, subscription);

            try {
                await Handler(context).NoContext();
                LastProcessed = EventPosition.FromContext(context);
                await Ack(context).NoContext();
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
                // Its own token was cancelled: the supervisor already knows the run is over.
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

    // ReSharper disable once MemberCanBeMadeStatic.Local
#pragma warning disable CA1822
    async ValueTask Ack(MessageConsumeContext ctx) {
#pragma warning restore CA1822
        var re           = ctx.Items.GetItem<ResolvedEvent>(ResolvedEventKey);
        var subscription = ctx.Items.GetItem<PersistentSubscription>(SubscriptionKey)!;
        await subscription.Ack(re).NoContext();
    }

    async ValueTask Nack(MessageConsumeContext ctx, Exception exception) {
        if (exception is OperationCanceledException && ctx.CancellationToken.IsCancellationRequested) {
            return;
        }

        // Handler-pipeline failures are already logged via context.Nack inside EventSubscription.Handler.
        // Anything else reaching here (e.g. an Ack failure after the handler returned) needs its own log entry.
        if (!ctx.HasFailed()) {
            ctx.LogContext.MessageHandlingFailed(Options.SubscriptionId, ctx, exception);
        }

        var re           = ctx.Items.GetItem<ResolvedEvent>(ResolvedEventKey);
        var subscription = ctx.Items.GetItem<PersistentSubscription>(SubscriptionKey)!;
        await _handleEventProcessingFailure(Client, subscription, re, exception).NoContext();
    }

    MessageConsumeContext CreateContext(SubscriptionRun run, ResolvedEvent re, CancellationToken cancellationToken) {
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
            run.NextSequence(),
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

    static Task DefaultEventProcessingFailureHandler(
            KurrentDBClient        client,
            PersistentSubscription subscription,
            ResolvedEvent          resolvedEvent,
            Exception              exception
        ) {
        // When ThrowOnError is enabled, Handler wraps the original exception in SubscriptionException;
        // unwrap it so the parked-message reason carries the actual handler error rather than a generic
        // "Error processing event ..." string.
        var cause = exception is SubscriptionException { InnerException: { } inner } ? inner : exception;

        return subscription.Nack(PersistentSubscriptionNakEventAction.Retry, cause.Message, resolvedEvent);
    }
}
