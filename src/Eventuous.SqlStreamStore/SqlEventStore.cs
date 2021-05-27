using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using JetBrains.Annotations;
using Eventuous;

namespace Eventuous.SqlStreamStore {
    [PublicAPI]
    public abstract class SqlEventStore : IEventStore {
        readonly IStreamStore _streamStore;        
        const int PageSize = 500;
        const string ContentType = "application/json";
        protected SqlEventStore(IStreamStore streamStore) => _streamStore = streamStore;
        
        public async Task<AppendEventsResult> AppendEvents(
            string                           stream,
            ExpectedStreamVersion            expectedVersion,
            IReadOnlyCollection<StreamEvent> events,
            CancellationToken                cancellationToken
        )
        {
            var proposedEvents = events.Select(ToStreamMessage).ToArray();

            Task<AppendResult> resultTask;

            if (expectedVersion == ExpectedStreamVersion.NoStream)
                resultTask = _streamStore.AppendToStream(
                    new StreamId(stream),
                    ExpectedVersion.NoStream,
                    proposedEvents,
                    cancellationToken
                );
            else if (expectedVersion == ExpectedStreamVersion.Any)
                resultTask = _streamStore.AppendToStream(
                    new StreamId(stream),
                    ExpectedVersion.Any,
                    proposedEvents,
                    cancellationToken
                );
            else
                resultTask = _streamStore.AppendToStream(
                    new StreamId(stream),
                    (int)expectedVersion.Value,
                    proposedEvents,
                    cancellationToken
                );

            var result = await resultTask.Ignore();

            return new AppendEventsResult(
                (ulong) result.CurrentPosition,
                (long) result.CurrentVersion + 1
            );

            static NewStreamMessage ToStreamMessage(StreamEvent streamEvent)
                => new(
                    Guid.NewGuid(),
                    streamEvent.EventType,
                    Encoding.UTF8.GetString(streamEvent.Data)
                );
        }

        public async Task<StreamEvent[]> ReadEvents(
            string              stream, 
            StreamReadPosition  start, 
            int                 count, 
            CancellationToken   cancellationToken)
        {
            try {
                var page = await _streamStore.ReadStreamForwards(new StreamId(stream), (int)start.Value, count, true, cancellationToken);
                return ToStreamEvents(page.Messages);
            }
            catch (InvalidOperationException) {
                throw new Exceptions.StreamNotFound(stream);
            }
        }

        public async Task<StreamEvent[]> ReadEventsBackwards(
            string              stream,
            int                 count,
            CancellationToken   cancellationToken
        )
        {
            try {
                var page = await _streamStore.ReadStreamBackwards(new StreamId(stream), StreamVersion.End, count, true, cancellationToken); 
                return ToStreamEvents(page.Messages);
            }
            catch (InvalidOperationException) {
                throw new Exceptions.StreamNotFound(stream);
            }
        }

        public async Task ReadStream(
            string              stream,
            StreamReadPosition  start,
            Action<StreamEvent> callback,
            CancellationToken   cancellationToken
        )
        {
            var startVersion = (int) start.Value;
            var streamId = new StreamId(stream);
            try {
                do {
                    var page = await _streamStore.ReadStreamForwards(streamId, (int) start.Value, PageSize);
                    startVersion = page.NextStreamVersion;
                    foreach (var message in page.Messages) {
                        callback(await ToStreamEvent(message));
                    }
                    if (page.IsEnd) break;
                } while (true);
            }
            catch (InvalidOperationException) {
                throw new Exceptions.StreamNotFound(stream);
            }
        }

        static async Task<StreamEvent> ToStreamEvent(StreamMessage streamMessage)
            => new(
                streamMessage.Type,
                Encoding.UTF8.GetBytes(await streamMessage.GetJsonData()),
                Encoding.UTF8.GetBytes(streamMessage.JsonMetadata),
                ContentType
            );

        static StreamEvent[] ToStreamEvents(StreamMessage[] streamMessages)
        {
            var tasks = streamMessages.Select(ToStreamEvent);
            Task.WhenAll(tasks);
            return tasks.Select(task => task.Result).ToArray();
        }
       
    }
}