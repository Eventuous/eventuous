using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;

namespace Eventuous.EventStoreDB {
    [PublicAPI]
    public class EsdbEventStore : IEventStore {
        readonly EventStoreClient _client;

        public EsdbEventStore(EventStoreClient client) => _client = Ensure.NotNull(client, nameof(client));

        public EsdbEventStore(EventStoreClientSettings clientSettings)
            : this(new EventStoreClient(Ensure.NotNull(clientSettings, nameof(clientSettings)))) { }

        public async Task<AppendEventsResult> AppendEvents(
            string                           stream,
            ExpectedStreamVersion            expectedVersion,
            IReadOnlyCollection<StreamEvent> events,
            CancellationToken                cancellationToken
        ) {
            var proposedEvents = events.Select(ToEventData);

            Task<IWriteResult> resultTask;

            if (expectedVersion == ExpectedStreamVersion.NoStream)
                resultTask = _client.AppendToStreamAsync(stream, StreamState.NoStream, proposedEvents, cancellationToken: cancellationToken);
            else if (expectedVersion == ExpectedStreamVersion.Any)
                resultTask = _client.AppendToStreamAsync(stream, StreamState.Any, proposedEvents, cancellationToken: cancellationToken);
            else
                resultTask = _client.AppendToStreamAsync(
                    stream,
                    StreamRevision.FromInt64(expectedVersion.Value),
                    proposedEvents,
                    cancellationToken: cancellationToken
                );

            var result = await resultTask.Ignore();

            return new AppendEventsResult(
                result.LogPosition.CommitPosition,
                result.NextExpectedStreamRevision.ToInt64()
            );

            static EventData ToEventData(StreamEvent streamEvent)
                => new(
                    Uuid.NewUuid(),
                    streamEvent.EventType,
                    streamEvent.Data,
                    streamEvent.Metadata
                );
        }

        public async Task<StreamEvent[]> ReadEvents(string stream, StreamReadPosition start, int count, CancellationToken cancellationToken) {
            var position = new StreamPosition((ulong) start.Value);
            var read     = _client.ReadStreamAsync(Direction.Forwards, stream, position, count);

            try {
                var resolvedEvents = await read.ToArrayAsync(cancellationToken).Ignore();
                return ToStreamEvents(resolvedEvents);
            }
            catch (StreamNotFoundException) {
                throw new Exceptions.StreamNotFound(stream);
            }
        }

        public async Task<StreamEvent[]> ReadEventsBackwards(string stream, int count, CancellationToken cancellationToken) {
            var read = _client.ReadStreamAsync(Direction.Backwards, stream, StreamPosition.End, count);

            try {
                var resolvedEvents = await read.ToArrayAsync(cancellationToken).Ignore();
                return ToStreamEvents(resolvedEvents);
            }
            catch (StreamNotFoundException) {
                throw new Exceptions.StreamNotFound(stream);
            }
        }

        public async Task ReadStream(
            string              stream,
            StreamReadPosition  start,
            Action<StreamEvent> callback,
            CancellationToken   cancellationToken
        ) {
            var position = new StreamPosition((ulong) start.Value);
            var read     = _client.ReadStreamAsync(Direction.Forwards, stream, position);

            try {
                await foreach (var re in read.IgnoreWithCancellation(cancellationToken)) {
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