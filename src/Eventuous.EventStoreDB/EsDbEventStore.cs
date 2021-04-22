using System;
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
            string                           stream,
            ExpectedStreamVersion            expectedVersion,
            IReadOnlyCollection<StreamEvent> events
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

        public async Task<StreamEvent[]> ReadEvents(string stream, StreamReadPosition start, int count) {
            var position = new StreamPosition((ulong) start.Value);
            var read     = _client.ReadStreamAsync(Direction.Forwards, stream, position, count);

            try {
                var resolvedEvents = await read.ToArrayAsync();
                return ToStreamEvents(resolvedEvents);
            }
            catch (StreamNotFoundException) {
                throw new Exceptions.StreamNotFound(stream);
            }
        }

        public async Task<StreamEvent[]> ReadEventsBackwards(string stream, int count) {
            var read = _client.ReadStreamAsync(Direction.Backwards, stream, StreamPosition.End, count);

            try {
                var resolvedEvents = await read.ToArrayAsync();
                return ToStreamEvents(resolvedEvents);
            }
            catch (StreamNotFoundException) {
                throw new Exceptions.StreamNotFound(stream);
            }
        }

        public async Task ReadStream(string stream, StreamReadPosition start, Action<StreamEvent> callback) {
            var position = new StreamPosition((ulong) start.Value);
            var read     = _client.ReadStreamAsync(Direction.Forwards, stream, position);

            try {
                await foreach (var re in read) {
                    callback(ToStreamEvent(re));
                }
            }
            catch (StreamNotFoundException) {
                throw new Exceptions.StreamNotFound(stream);
            }
        }

        static StreamEvent ToStreamEvent(ResolvedEvent resolvedEvent)
            => new(
                resolvedEvent.Event.EventType,
                resolvedEvent.Event.Data.ToArray(),
                resolvedEvent.Event.Metadata.ToArray(),
                resolvedEvent.Event.ContentType
            );

        static StreamEvent[] ToStreamEvents(ResolvedEvent[] resolvedEvents)
            => resolvedEvents.Select(ToStreamEvent).ToArray();
    }
}