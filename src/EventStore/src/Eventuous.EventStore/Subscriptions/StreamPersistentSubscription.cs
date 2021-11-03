using Eventuous.EventStore.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Persistent subscription for EventStoreDB, for a specific stream
/// </summary>
[PublicAPI]
public class StreamPersistentSubscription
    : EventStoreSubscriptionBase<StreamPersistentSubscriptionOptions>,
        IMeasuredSubscription {
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
        IMessageConsumer                    consumer,
        ILoggerFactory?                     loggerFactory = null
    ) : base(
        eventStoreClient,
        options,
        consumer,
        loggerFactory
    ) {
        Ensure.NotEmptyString(options.Stream, nameof(options.Stream));

        var settings   = eventStoreClient.GetSettings().Copy();
        var opSettings = settings.OperationOptions.Clone();
        options.ConfigureOperation?.Invoke(opSettings);
        settings.OperationOptions = opSettings;

        _subscriptionClient = new EventStorePersistentSubscriptionsClient(settings);

        _handleEventProcessingFailure =
            options.FailureHandler ?? DefaultEventProcessingFailureHandler;
    }

    /// <summary>
    /// Creates EventStoreDB persistent subscription service for a given stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="streamName">Name of the stream to receive events from</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="consumer"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer"></param>
    /// <param name="loggerFactory">Optional: logger factory</param>
    public StreamPersistentSubscription(
        EventStoreClient     eventStoreClient,
        StreamName           streamName,
        string               subscriptionId,
        IMessageConsumer     consumer,
        IEventSerializer?    eventSerializer = null,
        IMetadataSerializer? metaSerializer  = null,
        ILoggerFactory?      loggerFactory   = null
    ) : this(
        eventStoreClient,
        new StreamPersistentSubscriptionOptions {
            Stream             = streamName,
            SubscriptionId     = subscriptionId,
            EventSerializer    = eventSerializer,
            MetadataSerializer = metaSerializer
        },
        new FilterConsumer(consumer, re => !re.EventType.StartsWith("$")),
        loggerFactory
    ) { }

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var settings = Options.SubscriptionSettings
                    ?? new PersistentSubscriptionSettings(Options.ResolveLinkTos);

        var autoAck = Options.AutoAck;

        try {
            _subscription = await LocalSubscribe().NoContext();
        }
        catch (PersistentSubscriptionNotFoundException) {
            await _subscriptionClient.CreateAsync(
                    Options.Stream,
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
            var receivedEvent = CreateContext(re);

            try {
                await Handler(receivedEvent, ct).NoContext();

                if (!autoAck) await subscription.Ack(re).NoContext();

                LastProcessed = EventPosition.FromContext(receivedEvent);
            }
            catch (Exception e) {
                await _handleEventProcessingFailure(EventStoreClient, subscription, re, e)
                    .NoContext();
            }
        }

        Task<PersistentSubscription> LocalSubscribe()
            => _subscriptionClient.SubscribeAsync(
                Options.Stream,
                Options.SubscriptionId,
                HandleEvent,
                HandleDrop,
                Options.Credentials,
                Options.BufferSize,
                Options.AutoAck,
                cancellationToken
            );
    }

    IMessageConsumeContext CreateContext(ResolvedEvent re) {
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
            DeserializeMeta(re.Event.Metadata, re.OriginalStreamId, re.Event.EventNumber)
        ) {
            GlobalPosition = re.Event.Position.CommitPosition,
            StreamPosition = re.Event.EventNumber
        };
    }

    protected override ValueTask Unsubscribe(CancellationToken cancellationToken) {
        _subscription?.Dispose();
        return default;
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

    public ISubscriptionGapMeasure GetMeasure()
        => new StreamSubscriptionMeasure(
            Options.SubscriptionId,
            Options.Stream,
            EventStoreClient,
            Options.ResolveLinkTos,
            () => LastProcessed
        );
}
