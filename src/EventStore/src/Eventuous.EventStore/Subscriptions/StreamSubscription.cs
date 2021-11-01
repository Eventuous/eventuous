using Eventuous.EventStore.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Catch-up subscription for EventStoreDB, for a specific stream
/// </summary>
[PublicAPI]
public class StreamSubscription : EventStoreCatchUpSubscriptionBase<StreamSubscriptionOptions>,
    IMeasuredSubscription {
    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for a given stream
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="streamName">Name of the stream to receive events from</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="consumer"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer"></param>
    /// <param name="loggerFactory">Optional: logger factory</param>
    /// <param name="throwOnError"></param>
    public StreamSubscription(
        EventStoreClient     eventStoreClient,
        string               streamName,
        string               subscriptionId,
        ICheckpointStore     checkpointStore,
        IMessageConsumer     consumer,
        IEventSerializer?    eventSerializer = null,
        IMetadataSerializer? metaSerializer  = null,
        ILoggerFactory?      loggerFactory   = null,
        bool                 throwOnError    = false
    ) : this(
        eventStoreClient,
        new StreamSubscriptionOptions {
            StreamName         = streamName,
            SubscriptionId     = subscriptionId,
            ThrowOnError       = throwOnError,
            EventSerializer    = eventSerializer,
            MetadataSerializer = metaSerializer
        },
        checkpointStore,
        consumer,
        loggerFactory
    ) { }

    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for a given stream
    /// </summary>
    /// <param name="client"></param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="options">Subscription options</param>
    /// <param name="consumer"></param>
    /// <param name="loggerFactory">Optional: logger factory</param>
    public StreamSubscription(
        EventStoreClient          client,
        StreamSubscriptionOptions options,
        ICheckpointStore          checkpointStore,
        IMessageConsumer          consumer,
        ILoggerFactory?           loggerFactory = null
    ) : base(
        client,
        options,
        checkpointStore,
        consumer,
        loggerFactory
    )
        => Ensure.NotEmptyString(options.StreamName, nameof(options.StreamName));

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var (_, position) = await GetCheckpoint(cancellationToken);

        var subTask = position == null
            ? EventStoreClient.SubscribeToStreamAsync(
                Options.StreamName,
                HandleEvent,
                Options.ResolveLinkTos,
                HandleDrop,
                Options.ConfigureOperation,
                Options.Credentials,
                cancellationToken
            )
            : EventStoreClient.SubscribeToStreamAsync(
                Options.StreamName,
                StreamPosition.FromInt64((long)position),
                HandleEvent,
                Options.ResolveLinkTos,
                HandleDrop,
                Options.ConfigureOperation,
                Options.Credentials,
                cancellationToken
            );

        Subscription = await subTask.NoContext();

        async Task HandleEvent(
            global::EventStore.Client.StreamSubscription _,
            ResolvedEvent                                re,
            CancellationToken                            ct
        ) {
            // Despite ResolvedEvent.Event being not marked as nullable, it returns null for deleted events
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (re.Event is not null)
                await HandleInt(CreateContext(re), ct);
        }

        void HandleDrop(
            global::EventStore.Client.StreamSubscription _,
            SubscriptionDroppedReason                    reason,
            Exception?                                   ex
        )
            => Dropped(EsdbMappings.AsDropReason(reason), ex);
    }

    protected override IMessageConsumeContext CreateContext(ResolvedEvent re) {
        var evt = DeserializeData(
            re.Event.ContentType,
            re.Event.EventType,
            re.Event.Data,
            re.Event.EventStreamId,
            re.Event.EventNumber
        );

        return new MessageConsumeContext(
            re.Event.EventId.ToString(),
            re.Event.EventType,
            re.Event.ContentType,
            re.Event.EventStreamId,
            re.OriginalEventNumber.ToUInt64(),
            re.Event.Created,
            evt,
            DeserializeMeta(re.Event.Metadata, re.OriginalStreamId, re.Event.EventNumber)
        ) {
            GlobalPosition = re.Event.Position.CommitPosition,
            StreamPosition = re.Event.EventNumber
        };
    }

    public ISubscriptionGapMeasure GetMeasure()
        => new StreamSubscriptionMeasure(EventStoreClient, Options.ResolveLinkTos, () => LastProcessed);
}