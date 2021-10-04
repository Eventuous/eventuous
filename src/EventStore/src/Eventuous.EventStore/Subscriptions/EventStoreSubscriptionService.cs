using Microsoft.Extensions.Logging;

namespace Eventuous.EventStore.Subscriptions; 

[PublicAPI]
public abstract class EventStoreSubscriptionService : SubscriptionService {
    readonly  IMetadataSerializer _metaSerializer;
    protected EventStoreClient    EventStoreClient { get; }

    protected EventStoreSubscriptionService(
        EventStoreClient              eventStoreClient,
        EventStoreSubscriptionOptions options,
        ICheckpointStore              checkpointStore,
        IEnumerable<IEventHandler>    eventHandlers,
        ILoggerFactory?               loggerFactory   = null,
        ISubscriptionGapMeasure?      measure         = null
    ) : base(
        options,
        checkpointStore,
        eventHandlers,
        loggerFactory,
        measure
    ) {
        EventStoreClient = Ensure.NotNull(eventStoreClient, nameof(eventStoreClient));
        _metaSerializer  = options.MetadataSerializer ?? DefaultMetadataSerializer.Instance;
    }

    protected override async Task<EventPosition> GetLastEventPosition(
        CancellationToken cancellationToken
    ) {
        var read = EventStoreClient.ReadAllAsync(
            Direction.Backwards,
            Position.End,
            1,
            cancellationToken: cancellationToken
        );

        var events = await read.ToArrayAsync(cancellationToken).NoContext();

        return new EventPosition(
            events[0].Event.Position.CommitPosition,
            events[0].Event.Created
        );
    }
        

    protected Metadata? DeserializeMeta(
        ReadOnlyMemory<byte> meta,
        string               stream,
        ulong                position = 0
    ) {
        if (meta.IsEmpty) return null;

        try {
            return _metaSerializer.Deserialize(meta.Span);
        }
        catch (Exception e) {
            Log?.LogError(
                e,
                "Error deserializing metadata {Stream} {Position}",
                stream,
                position
            );

            if (FailOnError)
                throw new DeserializationException(stream, "metadata", position, e);

            return null;
        }
    }
}