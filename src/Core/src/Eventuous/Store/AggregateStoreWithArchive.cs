using static Eventuous.Diagnostics.EventuousEventSource;

namespace Eventuous;

public class AggregateStore<TReader> : IAggregateStore where TReader : IEventReader {
    readonly Func<StreamEvent, StreamEvent> _amendEvent;
    readonly AggregateFactoryRegistry       _factoryRegistry;
    readonly IEventReader                   _archiveReader;
    readonly IEventStore                    _eventStore;

    public AggregateStore(
        IEventStore                     eventStore,
        IEventReader                    archiveReader,
        Func<StreamEvent, StreamEvent>? amendEvent      = null,
        AggregateFactoryRegistry?       factoryRegistry = null
    ) {
        _amendEvent      = amendEvent      ?? (x => x);
        _factoryRegistry = factoryRegistry ?? AggregateFactoryRegistry.Instance;
        _eventStore      = Ensure.NotNull(eventStore);
        _archiveReader   = Ensure.NotNull(archiveReader);
    }

    public Task<AppendEventsResult> Store<T>(StreamName streamName, T aggregate, CancellationToken cancellationToken)
        where T : Aggregate
        => _eventStore.Store(streamName, aggregate, _amendEvent, cancellationToken);

    public Task<T> Load<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate
        => LoadInternal<T>(streamName, true, cancellationToken);

    public Task<T> LoadOrNew<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate
        => LoadInternal<T>(streamName, false, cancellationToken);

    async Task<T> LoadInternal<T>(StreamName streamName, bool failIfNotFound, CancellationToken cancellationToken)
        where T : Aggregate {
        var aggregate = _factoryRegistry.CreateInstance<T>();

        var hotEvents = await LoadStreamEvents(_eventStore, StreamReadPosition.Start);

        var archivedEvents =
            hotEvents.Length == 0 || hotEvents[0].Position > 0
                ? await LoadStreamEvents(_archiveReader, StreamReadPosition.Start)
                : Enumerable.Empty<StreamEvent>();

        var streamEvents = hotEvents.Concat(archivedEvents).Distinct(Comparer).ToArray();

        if (streamEvents.Length == 0 && failIfNotFound) {
            throw new AggregateNotFoundException<T>(streamName, new StreamNotFound(streamName));
        }

        foreach (var streamEvent in streamEvents) {
            Fold(streamEvent);
        }

        return aggregate;

        void Fold(StreamEvent streamEvent) {
            var evt = streamEvent.Payload;
            if (evt == null) return;

            aggregate.Fold(evt);
        }

        async Task<StreamEvent[]> LoadStreamEvents(IEventReader reader, StreamReadPosition start) {
            try {
                return await reader.ReadStream(
                    streamName,
                    start,
                    failIfNotFound,
                    cancellationToken
                );
            }
            catch (StreamNotFound) {
                return Array.Empty<StreamEvent>();
            }
            catch (Exception e) {
                Log.UnableToLoadAggregate<T>(streamName, e);
                throw;
            }
        }
    }

    static readonly StreamEventPositionComparer Comparer = new();

    class StreamEventPositionComparer : IEqualityComparer<StreamEvent> {
        public bool Equals(StreamEvent? x, StreamEvent? y) => x?.Position == y?.Position;

        public int GetHashCode(StreamEvent obj) => obj.Position.GetHashCode();
    }
}
