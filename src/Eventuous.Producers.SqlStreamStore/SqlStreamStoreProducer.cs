using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using SqlStreamStore;
using SqlStreamStore.Streams;
using Eventuous;
using Eventuous.Producers;

namespace Eventuous.Producers.SqlStreamStore
{
    /// <summary>
    /// Base class to create event stores on top of standard SQL databases. 
    /// It's based on the SqlStreamStore library (https://sqlstreamstore.readthedocs.io).
    /// </summary>
    public abstract class SqlStreamStoreProducer : BaseProducer<SqlStreamStoreProduceOptions>
    {
        protected readonly IStreamStore StreamStore;
        readonly IEventSerializer _serializer;
        const int ChunkSize = 500;

        /// <summary>
        /// Create a new SqlStreamStore producer instance
        /// </summary>
        /// <param name="streamStore">IStreamStore instance</param>
        /// <param name="serializer">Event serializer instance</param>
        protected SqlStreamStoreProducer(IStreamStore streamStore, IEventSerializer serializer) {
            StreamStore = Ensure.NotNull(streamStore, nameof(streamStore));
            _serializer = Ensure.NotNull(serializer, nameof(serializer));
        }
       
        public override Task Initialize(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public override Task Shutdown(CancellationToken cancellationToken = default) => Task.CompletedTask;

        protected override async Task ProduceMany(
            string                          stream,
            IEnumerable<object>             messages,
            SqlStreamStoreProduceOptions?   options,
            CancellationToken               cancellationToken
        ) {
            var data = Ensure.NotNull(messages, nameof(messages))
                .Select(x => CreateMessage(x, x.GetType(), options?.Metadata));

            foreach( var chunk in data.Chunks(ChunkSize)) {
                await StreamStore.AppendToStream(
                    new StreamId(stream),
                    options?.ExpectedState ?? ExpectedVersion.Any,
                    chunk.ToArray(),
                    cancellationToken
                );
            }
        }

        protected override Task ProduceOne(
            string                          stream,
            object                          message,
            Type                            type,
            SqlStreamStoreProduceOptions?   options,
            CancellationToken               cancellationToken
        ) {
            var eventData = CreateMessage(message, type, options?.Metadata);

            return StreamStore.AppendToStream(
                new StreamId(stream),
                options?.ExpectedState ?? ExpectedVersion.Any,
                new[] { eventData},
                cancellationToken
            );
        }

        NewStreamMessage CreateMessage(object message, Type type, object? metadata) {
            var msg = Ensure.NotNull(message, nameof(message));
            var typeName = TypeMap.GetTypeNameByType(type);
            var meta = metadata == null? null : Encoding.UTF8.GetString(_serializer.Serialize(metadata));
        
            return new NewStreamMessage(
                Guid.NewGuid(),
                typeName,
                Encoding.UTF8.GetString(_serializer.Serialize(msg)),
                meta
            );
        }
    }
}
