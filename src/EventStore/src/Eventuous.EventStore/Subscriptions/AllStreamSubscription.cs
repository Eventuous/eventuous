using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Monitoring;
using Microsoft.Extensions.Logging;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Catch-up subscription for EventStoreDB, using the $all global stream
/// </summary>
[PublicAPI]
public class AllStreamSubscription : EventStoreSubscriptionService<AllStreamSubscriptionOptions> {
    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for $all
    /// </summary>
    /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="eventSerializer">Event serializer instance</param>
    /// <param name="eventHandlers">Collection of event handlers</param>
    /// <param name="metaSerializer"></param>
    /// <param name="loggerFactory">Optional: logger factory</param>
    /// <param name="eventFilter">Optional: server-side event filter</param>
    /// <param name="measure">Optional: gap measurement for metrics</param>
    public AllStreamSubscription(
        EventStoreClient           eventStoreClient,
        string                     subscriptionId,
        ICheckpointStore           checkpointStore,
        IEnumerable<IEventHandler> eventHandlers,
        IEventSerializer?          eventSerializer = null,
        IMetadataSerializer?       metaSerializer  = null,
        ILoggerFactory?            loggerFactory   = null,
        IEventFilter?              eventFilter     = null,
        ISubscriptionGapMeasure?   measure         = null
    ) : this(
        eventStoreClient,
        new AllStreamSubscriptionOptions {
            SubscriptionId     = subscriptionId,
            EventSerializer    = eventSerializer,
            MetadataSerializer = metaSerializer,
            EventFilter        = eventFilter
        },
        checkpointStore,
        eventHandlers,
        loggerFactory,
        measure
    ) { }

    /// <summary>
    /// Creates EventStoreDB catch-up subscription service for $all
    /// </summary>
    /// <param name="eventStoreClient"></param>
    /// <param name="options"></param>
    /// <param name="checkpointStore">Checkpoint store instance</param>
    /// <param name="eventHandlers">Collection of event handlers</param>
    /// <param name="loggerFactory">Optional: logger factory</param>
    /// <param name="measure">Optional: gap measurement for metrics</param>
    public AllStreamSubscription(
        EventStoreClient             eventStoreClient,
        AllStreamSubscriptionOptions options,
        ICheckpointStore             checkpointStore,
        IEnumerable<IEventHandler>   eventHandlers,
        ILoggerFactory?              loggerFactory = null,
        ISubscriptionGapMeasure?     measure       = null
    ) : base(
        eventStoreClient,
        options,
        checkpointStore,
        eventHandlers,
        loggerFactory,
        measure
    ) { }

    protected override async Task<EventSubscription> Subscribe(
        Checkpoint        checkpoint,
        CancellationToken cancellationToken
    ) {
        var filterOptions = new SubscriptionFilterOptions(
            Options.EventFilter ?? EventTypeFilter.ExcludeSystemEvents(),
            10,
            (_, p, ct) => StoreCheckpoint(new EventPosition(p.CommitPosition, DateTime.Now), ct)
        );

        var (_, position) = checkpoint;

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

        var sub = await subTask.NoContext();

        return new EventSubscription(Options.SubscriptionId, new Stoppable(() => sub.Dispose()));

        Task HandleEvent(
            global::EventStore.Client.StreamSubscription _,
            ResolvedEvent                                re,
            CancellationToken                            ct
        )
            => Handler(AsReceivedEvent(re), ct);

        void HandleDrop(
            global::EventStore.Client.StreamSubscription _,
            SubscriptionDroppedReason                    reason,
            Exception?                                   ex
        )
            => Dropped(EsdbMappings.AsDropReason(reason), ex);

        ReceivedEvent AsReceivedEvent(ResolvedEvent re) {
            var evt = DeserializeData(
                re.Event.ContentType,
                re.Event.EventType,
                re.Event.Data,
                re.Event.EventStreamId,
                re.Event.EventNumber
            );

            return new ReceivedEvent(
                re.Event.EventId.ToString(),
                re.Event.EventType,
                re.Event.ContentType,
                re.Event.Position.CommitPosition,
                re.Event.Position.CommitPosition,
                re.OriginalStreamId,
                _sequence++,
                re.Event.Created,
                evt,
                DeserializeMeta(re.Event.Metadata, re.OriginalStreamId)
            );
        }
    }

    ulong _sequence;
}