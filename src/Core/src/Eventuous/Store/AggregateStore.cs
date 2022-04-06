using static Eventuous.Diagnostics.EventuousEventSource;

namespace Eventuous;

public delegate Metadata? GetEventMetadata(string stream, object evt);

[PublicAPI]
public class AggregateStore : IAggregateStore {
    readonly GetEventMetadata?        _getEventMetadata;
    readonly AggregateFactoryRegistry _factoryRegistry;
    readonly IEventStore              _eventStore;

    /// <summary>
    /// Creates a new instance of the default aggregate store
    /// </summary>
    /// <param name="eventStore">Event store implementation</param>
    /// <param name="getEventMetadata">Optional: a function to produce metadata</param>
    /// <param name="factoryRegistry"></param>
    public AggregateStore(
        IEventStore               eventStore,
        GetEventMetadata?         getEventMetadata = null,
        AggregateFactoryRegistry? factoryRegistry  = null
    ) {
        _getEventMetadata = getEventMetadata;
        _factoryRegistry  = factoryRegistry ?? AggregateFactoryRegistry.Instance;
        _eventStore       = Ensure.NotNull(eventStore);
    }

    public async Task<AppendEventsResult> Store<T>(
        StreamName        streamName,
        T                 aggregate,
        CancellationToken cancellationToken
    )
        where T : Aggregate {
        Ensure.NotNull(aggregate);

        if (aggregate.Changes.Count == 0) return AppendEventsResult.NoOp;

        var expectedVersion = new ExpectedStreamVersion(aggregate.OriginalVersion);

        try {
            var result = await _eventStore.AppendEvents(
                    streamName,
                    expectedVersion,
                    aggregate.Changes.Select(ToStreamEvent).ToArray(),
                    cancellationToken
                )
                .NoContext();

            return result;
        }
        catch (Exception e) {
            Log.UnableToStoreAggregate(aggregate, e);

            throw e.InnerException?.Message.Contains("WrongExpectedVersion") == true
                ? new OptimisticConcurrencyException<T>(aggregate, e) : e;
        }

        StreamEvent ToStreamEvent(object evt) {
            var meta = _getEventMetadata?.Invoke(streamName, evt) ?? new Metadata();
            return new StreamEvent(evt, meta, "", -1);
        }
    }

    public async Task<T> Load<T>(StreamName streamName, CancellationToken cancellationToken)
        where T : Aggregate {
        const int pageSize = 500;

        var aggregate = _factoryRegistry.CreateInstance<T>();

        try {
            var position = StreamReadPosition.Start;

            while (true) {
                var readCount = await _eventStore.ReadStream(
                        streamName,
                        position,
                        pageSize,
                        Fold,
                        cancellationToken
                    )
                    .NoContext();

                if (readCount < pageSize) break;

                position = new StreamReadPosition(position.Value + readCount);
            }
        }
        catch (Exception e) {
            Log.UnableToLoadAggregate<T>(streamName, e);
            throw e is StreamNotFound ? new AggregateNotFoundException<T>(streamName, e) : e;
        }

        return aggregate;

        void Fold(StreamEvent streamEvent) {
            var evt = streamEvent.Payload;
            if (evt == null) return;

            aggregate.Fold(evt);
        }
    }

    public Task<bool> Exists<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate
        => _eventStore.StreamExists(streamName, cancellationToken);
}
