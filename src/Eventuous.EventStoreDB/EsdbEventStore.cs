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

        public EsdbEventStore(EventStoreClient client)
            => _client = Ensure.NotNull(client, nameof(client));

        public EsdbEventStore(EventStoreClientSettings clientSettings)
            : this(new EventStoreClient(Ensure.NotNull(clientSettings, nameof(clientSettings)))) { }

        public async Task<AppendEventsResult> AppendEvents(
            StreamName                       stream,
            ExpectedStreamVersion            expectedVersion,
            IReadOnlyCollection<StreamEvent> events,
            CancellationToken                cancellationToken
        ) {
            var proposedEvents = events.Select(ToEventData);

            Task<IWriteResult> resultTask = expectedVersion == ExpectedStreamVersion.NoStream
                ? _client.AppendToStreamAsync(
                    stream,
                    StreamState.NoStream,
                    proposedEvents,
                    cancellationToken: cancellationToken
                ) : AnyOrNot(
                    expectedVersion,
                    () => _client.AppendToStreamAsync(
                        stream,
                        StreamState.Any,
                        proposedEvents,
                        cancellationToken: cancellationToken
                    ),
                    () => _client.AppendToStreamAsync(
                        stream,
                        expectedVersion.AsStreamRevision(),
                        proposedEvents,
                        cancellationToken: cancellationToken
                    )
                );

            var result = await resultTask.NoContext();

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

        public async Task<StreamEvent[]> ReadEvents(
            StreamName         stream,
            StreamReadPosition start,
            int                count,
            CancellationToken  cancellationToken
        ) {
            var read = _client.ReadStreamAsync(
                Direction.Forwards,
                stream,
                start.AsStreamPosition(),
                count,
                cancellationToken: cancellationToken
            );

            try {
                var resolvedEvents = await read.ToArrayAsync(cancellationToken).NoContext();
                return ToStreamEvents(resolvedEvents);
            }
            catch (StreamNotFoundException) {
                throw new Exceptions.StreamNotFound(stream);
            }
        }

        public async Task<StreamEvent[]> ReadEventsBackwards(
            StreamName        stream,
            int               count,
            CancellationToken cancellationToken
        ) {
            var read = _client.ReadStreamAsync(
                Direction.Backwards,
                stream,
                StreamPosition.End,
                count,
                cancellationToken: cancellationToken
            );

            try {
                var resolvedEvents = await read.ToArrayAsync(cancellationToken).NoContext();
                return ToStreamEvents(resolvedEvents);
            }
            catch (StreamNotFoundException) {
                throw new Exceptions.StreamNotFound(stream);
            }
        }

        public async Task ReadStream(
            StreamName          stream,
            StreamReadPosition  start,
            Action<StreamEvent> callback,
            CancellationToken   cancellationToken
        ) {
            var read = _client.ReadStreamAsync(
                Direction.Forwards,
                stream,
                start.AsStreamPosition(),
                cancellationToken: cancellationToken
            );

            try {
                await foreach (var re in read.IgnoreWithCancellation(cancellationToken)) {
                    callback(ToStreamEvent(re));
                }
            }
            catch (StreamNotFoundException) {
                throw new Exceptions.StreamNotFound(stream);
            }
        }

        public Task TruncateStream(
            StreamName             stream,
            StreamTruncatePosition truncatePosition,
            ExpectedStreamVersion  expectedVersion,
            CancellationToken      cancellationToken
        ) {
            var meta = new StreamMetadata(truncateBefore: truncatePosition.AsStreamPosition());

            return AnyOrNot(
                expectedVersion,
                () => _client.SetStreamMetadataAsync(
                    stream,
                    StreamState.Any,
                    meta,
                    cancellationToken: cancellationToken
                ),
                () => _client.SetStreamMetadataAsync(
                    stream,
                    expectedVersion.AsStreamRevision(),
                    meta,
                    cancellationToken: cancellationToken
                )
            );
        }

        public Task DeleteStream(
            StreamName            stream,
            ExpectedStreamVersion expectedVersion,
            CancellationToken     cancellationToken
        )
            => AnyOrNot(
                expectedVersion,
                () => _client.SoftDeleteAsync(
                    stream,
                    StreamState.Any,
                    cancellationToken: cancellationToken
                ),
                () => _client.SoftDeleteAsync(
                    stream,
                    expectedVersion.AsStreamRevision(),
                    cancellationToken: cancellationToken
                )
            );

        static Task<T> AnyOrNot<T>(
            ExpectedStreamVersion version,
            Func<Task<T>>         whenAny,
            Func<Task<T>>         otherwise
        )
            => version == ExpectedStreamVersion.Any ? whenAny() : otherwise();

        static StreamEvent ToStreamEvent(ResolvedEvent resolvedEvent)
            => new(
                resolvedEvent.Event.EventType,
                resolvedEvent.Event.Data.ToArray(),
                resolvedEvent.Event.Metadata.ToArray(),
                resolvedEvent.Event.ContentType,
                resolvedEvent.OriginalEventNumber.ToInt64()
            );

        static StreamEvent[] ToStreamEvents(ResolvedEvent[] resolvedEvents)
            => resolvedEvents.Select(ToStreamEvent).ToArray();
    }
}
