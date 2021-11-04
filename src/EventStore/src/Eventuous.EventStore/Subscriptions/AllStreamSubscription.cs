using Eventuous.EventStore.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Catch-up subscription for EventStoreDB, using the $all global stream
/// </summary>
[PublicAPI]
public class AllStreamSubscription
    : EventStoreCatchUpSubscriptionBase<AllStreamSubscriptionOptions>, IMeasuredSubscription {
    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for $all
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="consumer"></param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="metaSerializer"></param>
    /// <param name="loggerFactory">Optional: logger factory</param>
    /// <param name="eventFilter">Optional: server-side event filter</param>
    public AllStreamSubscription(
        EventStoreClient     eventStoreClient,
        string               subscriptionId,
        ICheckpointStore     checkpointStore,
        IMessageConsumer     consumer,
        IEventSerializer?    eventSerializer = null,
        IMetadataSerializer? metaSerializer  = null,
        ILoggerFactory?      loggerFactory   = null,
        IEventFilter?        eventFilter     = null
    ) : this(
        eventStoreClient,
        new AllStreamSubscriptionOptions {
            SubscriptionId     = subscriptionId,
            EventSerializer    = eventSerializer,
            MetadataSerializer = metaSerializer,
            EventFilter        = eventFilter
        },
        checkpointStore,
        consumer,
        loggerFactory
    ) { }

    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for $all
    /// </summary>
    /// <param name="eventStoreClient"></param>
    /// <param name="options"></param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="consumer"></param>
    /// <param name="loggerFactory">Optional: logger factory</param>
    public AllStreamSubscription(
        EventStoreClient             eventStoreClient,
        AllStreamSubscriptionOptions options,
        ICheckpointStore             checkpointStore,
        IMessageConsumer             consumer,
        ILoggerFactory?              loggerFactory = null
    ) : base(
        eventStoreClient,
        options,
        checkpointStore,
        consumer,
        loggerFactory
    ) { }

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var filterOptions = new SubscriptionFilterOptions(
            Options.EventFilter ?? EventTypeFilter.ExcludeSystemEvents(),
            Options.CheckpointInterval,
            async (_, p, ct)
                => await StoreCheckpoint(
                    new EventPosition(p.CommitPosition, DateTime.Now),
                    ct
                )
        );

        var (_, position) = await GetCheckpoint(cancellationToken);

        var subTask = position != null
            ? EventStoreClient.SubscribeToAllAsync(
                new Position(position.Value, position.Value),
                HandleEvent,
                false,
                HandleDrop,
                filterOptions,
                Options.ConfigureOperation,
                Options.Credentials,
                cancellationToken
            )
            : EventStoreClient.SubscribeToAllAsync(
                HandleEvent,
                false,
                HandleDrop,
                filterOptions,
                Options.ConfigureOperation,
                Options.Credentials,
                cancellationToken
            );

        Subscription = await subTask.NoContext();

        async Task HandleEvent(
            global::EventStore.Client.StreamSubscription _,
            ResolvedEvent                                re,
            CancellationToken                            ct
        )
            => await HandleInternal(CreateContext(re), ct);

        void HandleDrop(
            global::EventStore.Client.StreamSubscription _,
            SubscriptionDroppedReason                    reason,
            Exception?                                   ex
        )
            => Dropped(EsdbMappings.AsDropReason(reason), ex);
    }

    IMessageConsumeContext CreateContext(ResolvedEvent re) {
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
            re.OriginalStreamId,
            _sequence++,
            re.Event.Created,
            evt,
            DeserializeMeta(re.Event.Metadata, re.OriginalStreamId)
        ) {
            GlobalPosition = re.Event.Position.CommitPosition,
            StreamPosition = re.Event.Position.CommitPosition
        };
    }

    ulong _sequence;

    public GetSubscriptionGap GetMeasure()
        => new AllStreamSubscriptionMeasure(
            Options.SubscriptionId,
            EventStoreClient,
            () => LastProcessed
        ).GetSubscriptionGap;
}