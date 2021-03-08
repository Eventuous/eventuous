using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;

namespace Eventuous.EventStoreDB {
    [PublicAPI]
    public class EsDbEventStore : IEventStore {
        readonly EventStoreClient _client;

        public EsDbEventStore(EventStoreClient client) => _client = client;

        public async Task AppendEvents(
            string stream, ExpectedStreamVersion expectedVersion, IReadOnlyCollection<StreamEvent> events
        ) {
            var proposedEvents = events.Select(ToEventData);

            var resultTask = expectedVersion == ExpectedStreamVersion.NoStream
                ? _client.AppendToStreamAsync(stream, StreamState.NoStream, proposedEvents)
                : _client.AppendToStreamAsync(stream, StreamRevision.FromInt64(expectedVersion.Value), proposedEvents);
            await resultTask;

            static EventData ToEventData(StreamEvent streamEvent)
                => new(
                    Uuid.NewUuid(),
                    streamEvent.EventType,
                    streamEvent.Data,
                    streamEvent.Metadata
                );
        }

        public async Task<StreamEvent[]> ReadEvents(string stream, StreamReadPosition start) {
            var position       = new StreamPosition((ulong) start.Value);
            var read           = _client.ReadStreamAsync(Direction.Forwards, stream, position);
            var resolvedEvents = await read.ToArrayAsync();

            return resolvedEvents
                .Select(x => new StreamEvent(x.Event.EventType, x.Event.Data.ToArray(), x.Event.Metadata.ToArray()))
                .ToArray();
        }
    }
}