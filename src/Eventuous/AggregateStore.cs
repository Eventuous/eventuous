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

            var stream          = StreamName.For<T>(aggregate.GetId());
            var expectedVersion = new ExpectedStreamVersion(aggregate.Version);

            await _eventStore.AppendEvents(stream, expectedVersion, aggregate.Changes.Select(ToStreamEvent).ToArray());

            StreamEvent ToStreamEvent(object evt)
                => new(TypeMap.GetTypeName(evt), _serializer.Serialize(evt));
        }

        public async Task<T> Load<T>(string id) where T : Aggregate, new() {
            if (id == null) throw new ArgumentNullException(nameof(id));

            var stream    = StreamName.For<T>(id);
            var aggregate = new T();

            try {
                await _eventStore.ReadStream(stream, StreamReadPosition.Start, Fold);
            }
            catch (Exceptions.StreamNotFound e) {
                throw new Exceptions.AggregateNotFound<T>(id, e);
            }

            return aggregate;

            void Fold(StreamEvent streamEvent) {
                var evt = Deserialize(streamEvent);
                if (evt == null) return;

                aggregate!.Fold(evt);
            }

            object? Deserialize(StreamEvent streamEvent)
                => _serializer.Deserialize(streamEvent.Data.AsSpan(), streamEvent.EventType);
        }
    }
}