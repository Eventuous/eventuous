using Microsoft.Extensions.Logging;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Persistent subscription for EventStoreDB, for a specific stream
/// </summary>
[PublicAPI]
public class StreamPersistentSubscription : EventStoreSubscriptionService {
    public delegate Task HandleEventProcessingFailure(
        EventStoreClient       client,
        PersistentSubscription subscription,
        ResolvedEvent          resolvedEvent,
        Exception              exception
    );

    readonly EventStorePersistentSubscriptionsClient _subscriptionClient;
    readonly StreamPersistentSubscriptionOptions     _options;
    readonly HandleEventProcessingFailure            _handleEventProcessingFailure;

    public StreamPersistentSubscription(
        EventStoreClient                    eventStoreClient,
        StreamPersistentSubscriptionOptions options,
        IEnumerable<IEventHandler>          eventHandlers,
        ILoggerFactory?                     loggerFactory = null,
        ISubscriptionGapMeasure?            measure       = null
    ) : base(
        eventStoreClient,
        options,
        new NoOpCheckpointStore(),
        eventHandlers,
        loggerFactory,
        measure
    ) {
        Ensure.NotEmptyString(options.Stream, nameof(options.Stream));

        var settings   = eventStoreClient.GetSettings().Copy();
        var opSettings = settings.OperationOptions.Clone();
        options.ConfigureOperation?.Invoke(opSettings);
        settings.OperationOptions = opSettings;

        _subscriptionClient = new EventStorePersistentSubscriptionsClient(settings);

        _handleEventProcessingFailure =
            options.FailureHandler ?? DefaultEventProcessingFailureHandler;

        _options = options;
    }

    /// <summary>
    /// Creates EventStoreDB persistent subscription service for a given stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="streamName">Name of the stream to receive events from</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer"></param>
    /// <param name="eventHandlers">Collection of event handlers</param>
    /// <param name="loggerFactory">Optional: logger factory</param>
    /// <param name="measure">Optional: gap measurement for metrics</param>
    public StreamPersistentSubscription(
        EventStoreClient           eventStoreClient,
        string                     streamName,
        string                     subscriptionId,
        IEnumerable<IEventHandler> eventHandlers,
        IEventSerializer?          eventSerializer = null,
        IMetadataSerializer?       metaSerializer  = null,
        ILoggerFactory?            loggerFactory   = null,
        ISubscriptionGapMeasure?   measure         = null
    ) : this(
        eventStoreClient,
        new StreamPersistentSubscriptionOptions {
            Stream             = streamName,
            SubscriptionId     = subscriptionId,
            EventSerializer    = eventSerializer,
            MetadataSerializer = metaSerializer
        },
        eventHandlers,
        loggerFactory,
        measure
    ) { }

    protected override async Task<EventSubscription> Subscribe(
        Checkpoint        _,
        CancellationToken cancellationToken
    ) {
        var settings = _options.SubscriptionSettings
                    ?? new PersistentSubscriptionSettings(_options.ResolveLinkTos);

        var autoAck = _options.AutoAck;

        PersistentSubscription sub;

        try {
            sub = await LocalSubscribe().NoContext();
        }
        catch (PersistentSubscriptionNotFoundException) {
            await _subscriptionClient.CreateAsync(
                    _options.Stream,
                    SubscriptionId,
                    settings,
                    _options.Credentials,
                    cancellationToken
                )
                .NoContext();

            sub = await LocalSubscribe().NoContext();
        }

        return new EventSubscription(SubscriptionId, new Stoppable(() => sub.Dispose()));

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
            var receivedEvent = AsReceivedEvent(re);

            try {
                await Handler(receivedEvent, ct).NoContext();

                if (!autoAck) await subscription.Ack(re).NoContext();
            }
            catch (Exception e) {
                await _handleEventProcessingFailure(EventStoreClient, subscription, re, e)
                    .NoContext();
            }
        }

        Task<PersistentSubscription> LocalSubscribe()
            => _subscriptionClient.SubscribeAsync(
                _options.Stream,
                SubscriptionId,
                HandleEvent,
                HandleDrop,
                _options.Credentials,
                _options.BufferSize,
                _options.AutoAck,
                cancellationToken
            );

        ReceivedEvent AsReceivedEvent(ResolvedEvent re) {
            var evt = DeserializeData(
                re.Event.ContentType,
                re.Event.EventType,
                re.Event.Data,
                re.OriginalStreamId,
                re.Event.Position.CommitPosition
            );

            return new ReceivedEvent(
                re.Event.EventId.ToString(),
                re.Event.EventType,
                re.Event.ContentType,
                re.Event.Position.CommitPosition,
                re.Event.EventNumber,
                re.OriginalStreamId,
                re.Event.EventNumber,
                re.Event.Created,
                evt,
                DeserializeMeta(re.Event.Metadata, re.OriginalStreamId, re.Event.EventNumber)
            );
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