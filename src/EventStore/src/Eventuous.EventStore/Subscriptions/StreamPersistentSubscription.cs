using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Eventuous.EventStore.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Persistent subscription for EventStoreDB, for a specific stream
/// </summary>
[PublicAPI]
public class StreamPersistentSubscription
    : EventStoreSubscriptionBase<StreamPersistentSubscriptionOptions>, IMeasuredSubscription {
    public delegate Task HandleEventProcessingFailure(
        EventStoreClient       client,
        PersistentSubscription subscription,
        ResolvedEvent          resolvedEvent,
        Exception              exception
    );

    readonly EventStorePersistentSubscriptionsClient _subscriptionClient;
    readonly HandleEventProcessingFailure            _handleEventProcessingFailure;

    PersistentSubscription? _subscription;

    public StreamPersistentSubscription(
        EventStoreClient                    eventStoreClient,
        StreamPersistentSubscriptionOptions options,
        ConsumePipe                         consumePipe
    ) : base(eventStoreClient, options, ConfigurePipe(consumePipe, options)) {
        Ensure.NotEmptyString(options.StreamName);

        var settings   = eventStoreClient.GetSettings().Copy();
        var opSettings = settings.OperationOptions.Clone();
        options.ConfigureOperation?.Invoke(opSettings);
        settings.OperationOptions = opSettings;

        _subscriptionClient = new EventStorePersistentSubscriptionsClient(settings);

        _handleEventProcessingFailure = options.FailureHandler ?? DefaultEventProcessingFailureHandler;

        if (options.FailureHandler != null && !options.ThrowOnError)
            Log.ThrowOnErrorIncompatible(SubscriptionId);
    }

    static ConsumePipe ConfigurePipe(ConsumePipe pipe, StreamPersistentSubscriptionOptions options) {
        return pipe;
        
        // if (options.AutoAck && options.ConcurrencyLevel > 1) {
        //     throw new ArgumentException(
        //         "Concurrency is not supported when auto-ack is enabled",
        //         nameof(options.ConcurrencyLevel)
        //     );
        // }
        //
        // return options.AutoAck ? pipe : pipe.AddFilterFirst(new ConcurrentFilter(options.ConcurrencyLevel));
    }

    /// <summary>
    /// Creates EventStoreDB persistent subscription service for a given stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="streamName">Name of the stream to receive events from</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="consumerPipe"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer"></param>
    public StreamPersistentSubscription(
        EventStoreClient     eventStoreClient,
        StreamName           streamName,
        string               subscriptionId,
        ConsumePipe          consumerPipe,
        IEventSerializer?    eventSerializer = null,
        IMetadataSerializer? metaSerializer  = null
    ) : this(
        eventStoreClient,
        new StreamPersistentSubscriptionOptions {
            StreamName         = streamName,
            SubscriptionId     = subscriptionId,
            EventSerializer    = eventSerializer,
            MetadataSerializer = metaSerializer
        },
        consumerPipe
    ) { }

    const string ResolvedEventKey = "resolvedEvent";
    const string SubscriptionKey  = "subscription";

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var settings = Options.SubscriptionSettings ?? new PersistentSubscriptionSettings(Options.ResolveLinkTos);
        var autoAck  = Options.AutoAck;

        try {
            _subscription = await LocalSubscribe().NoContext();
        }
        catch (PersistentSubscriptionNotFoundException) {
            await _subscriptionClient.CreateAsync(
                    Options.StreamName,
                    Options.SubscriptionId,
                    settings,
                    Options.Credentials,
                    cancellationToken
                )
                .NoContext();

            _subscription = await LocalSubscribe().NoContext();
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
            var context = CreateContext(re, ct)
                .WithItem(ResolvedEventKey, re)
                .WithItem(SubscriptionKey, subscription);

            try {
                // var ctx = autoAck ? context : new DelayedAckConsumeContext(context, Ack, Nack);

                await Handler(context).NoContext();
                LastProcessed = EventPosition.FromContext(context);
                if (!autoAck) await Ack(context);
            }
            catch (Exception e) {
                await Nack(context, e).NoContext();
            }
        }

        Task<PersistentSubscription> LocalSubscribe()
            => _subscriptionClient.SubscribeAsync(
                Options.StreamName,
                Options.SubscriptionId,
                HandleEvent,
                HandleDrop,
                Options.Credentials,
                Options.BufferSize,
                Options.AutoAck,
                cancellationToken
            );
    }

    ConcurrentQueue<ResolvedEvent> AckQueue { get; } = new();

    async ValueTask Ack(IMessageConsumeContext ctx) {
        var re = ctx.Items.TryGetItem<ResolvedEvent>(ResolvedEventKey);
        AckQueue.Enqueue(re);

        if (AckQueue.Count < Options.BufferSize) return;

        var subscription = ctx.Items.TryGetItem<PersistentSubscription>(SubscriptionKey)!;

        var toAck = new List<ResolvedEvent>();
        for (var i = 0; i < Options.BufferSize; i++) {
            if (AckQueue.TryDequeue(out var evt))
                toAck.Add(evt);
        }
        await subscription.Ack(toAck).NoContext();
    }

    async ValueTask Nack(IMessageConsumeContext ctx, Exception exception) {
        if (Options.ThrowOnError) throw exception;

        var re           = ctx.Items.TryGetItem<ResolvedEvent>(ResolvedEventKey);
        var subscription = ctx.Items.TryGetItem<PersistentSubscription>(SubscriptionKey)!;
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
                re.Event.EventNumber,
                re.Event.Created,
                evt,
                DeserializeMeta(re.Event.Metadata, re.OriginalStreamId, re.Event.EventNumber),
                SubscriptionId,
                cancellationToken
            )
            .WithItem(ContextKeys.GlobalPosition, re.Event.Position.CommitPosition)
            .WithItem(ContextKeys.StreamPosition, re.Event.EventNumber);
    }

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

    public GetSubscriptionGap GetMeasure()
        => new StreamSubscriptionMeasure(
            Options.SubscriptionId,
            Options.StreamName,
            EventStoreClient,
            () => LastProcessed
        ).GetSubscriptionGap;
}