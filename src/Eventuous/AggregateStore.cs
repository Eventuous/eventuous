using System;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous {
    [PublicAPI]
    public class AggregateStore : IAggregateStore {
        readonly IEventStore      _eventStore;
        readonly IEventSerializer _serializer;

        public AggregateStore(IEventStore eventStore, IEventSerializer serializer) {
            _eventStore = eventStore;
            _serializer = serializer;
        }

        public async Task Store<T>(T aggregate)
            where T : Aggregate {
            if (aggregate == null) throw new ArgumentNullException(nameof(aggregate));

            if (aggregate.Changes.Count == 0) return;

            var stream          = GetStreamName<T>(aggregate.GetId());
            var expectedVersion = new ExpectedStreamVersion(aggregate.Version);

            await _eventStore.AppendEvents(stream, expectedVersion, aggregate.Changes.Select(ToStreamEvent).ToArray());

            StreamEvent ToStreamEvent(object evt)
                => new(TypeMap.GetTypeName(evt), _serializer.Serialize(evt));
        }

        public async Task<T> Load<T>(string id) where T : Aggregate, new() {
            if (id == null) throw new ArgumentNullException(nameof(id));

            var stream    = GetStreamName<T>(id);
            var aggregate = new T();

            var events = await _eventStore.ReadEvents(stream, StreamReadPosition.Start);

            aggregate!.Load(events.Select(Deserialize));

            return aggregate;

            object? Deserialize(StreamEvent streamEvent) => _serializer.Deserialize(
                streamEvent.Data.AsSpan(),
                streamEvent.EventType
            );
        }

        protected virtual string GetStreamName<T>(string entityId) => $"{typeof(T).Name}-{entityId}";
    }
}