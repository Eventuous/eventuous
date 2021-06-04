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

        public AggregateStore(IEventStore eventStore, IEventSerializer? serializer = null) {
            _eventStore = Ensure.NotNull(eventStore, nameof(eventStore));
            _serializer = serializer ?? DefaultEventSerializer.Instance;
        }

        public async Task<AppendEventsResult> Store<T>(T aggregate, CancellationToken cancellationToken)
            where T : Aggregate {
            Ensure.NotNull(aggregate, nameof(aggregate));

            if (aggregate.Changes.Count == 0) return AppendEventsResult.NoOp;

            var stream          = StreamName.For<T>(aggregate.GetId());
            var expectedVersion = new ExpectedStreamVersion(aggregate.OriginalVersion);

            var result = await _eventStore.AppendEvents(
                stream,
                expectedVersion,
                aggregate.Changes.Select(ToStreamEvent).ToArray(),
                cancellationToken
            ).NoContext();

            return result;

            StreamEvent ToStreamEvent(object evt)
                => new(TypeMap.GetTypeName(evt), _serializer.Serialize(evt), null, _serializer.ContentType);
        }

        public async Task<T> Load<T>(string id, CancellationToken cancellationToken) where T : Aggregate, new() {
            Ensure.NotEmptyString(id, nameof(id));

            var stream    = StreamName.For<T>(id);
            var aggregate = new T();

            try {
                await _eventStore.ReadStream(stream, StreamReadPosition.Start, Fold, cancellationToken).NoContext();
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