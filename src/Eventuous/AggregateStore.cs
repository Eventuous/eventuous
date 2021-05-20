using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous {
    [PublicAPI]
    public class AggregateStore : IAggregateStore {
        readonly IEventStore      _eventStore;
        readonly IEventSerializer _serializer;

        public AggregateStore(IEventStore eventStore, IEventSerializer serializer) {
            _eventStore = Ensure.NotNull(eventStore, nameof(eventStore));
            _serializer = Ensure.NotNull(serializer, nameof(serializer));
        }

        public async Task Store<T>(T aggregate, CancellationToken cancellationToken)
            where T : Aggregate {
            Ensure.NotNull(aggregate, nameof(aggregate));

            if (aggregate.Changes.Count == 0) return;

            var stream          = StreamName.For<T>(aggregate.GetId());
            var expectedVersion = new ExpectedStreamVersion(aggregate.OriginalVersion);

            await _eventStore.AppendEvents(
                stream,
                expectedVersion,
                aggregate.Changes.Select(ToStreamEvent).ToArray(),
                cancellationToken
            ).Ignore();

            StreamEvent ToStreamEvent(object evt)
                => new(TypeMap.GetTypeName(evt), _serializer.Serialize(evt), null, _serializer.ContentType);
        }

        public async Task<T> Load<T>(string id, CancellationToken cancellationToken) where T : Aggregate, new() {
            Ensure.NotEmptyString(id, nameof(id));

            var stream    = StreamName.For<T>(id);
            var aggregate = new T();

            try {
                await _eventStore.ReadStream(stream, StreamReadPosition.Start, Fold, cancellationToken).Ignore();
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
        }

        public async Task<T> LoadState<T, TId>(StreamName stream, CancellationToken cancellationToken)
            where T : AggregateState<T, TId>, new() where TId : AggregateId {
            var state = new T();

            try {
                await _eventStore.ReadStream(stream, StreamReadPosition.Start, Fold, cancellationToken).Ignore();
            }
            catch (Exceptions.StreamNotFound e) {
                throw new Exceptions.StreamNotFound(stream);
            }

            return state;
            
            void Fold(StreamEvent streamEvent) {
                var evt = Deserialize(streamEvent);
                if (evt == null) return;

                state = state.When(evt);
            }
        }

        object? Deserialize(StreamEvent streamEvent)
            => _serializer.Deserialize(streamEvent.Data.AsSpan(), streamEvent.EventType);
    }
}