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
using JetBrains.Annotations;

namespace Eventuous.Producers.SqlStreamStore
{
    /// <summary>
    /// Producer for SqlStreamStore (https://sqlstreamstore.readthedocs.io)
    /// </summary>
    [PublicAPI]
    public class SqlStreamStoreProducer : BaseProducer<SqlStreamStoreProduceOptions>
    {
        readonly IStreamStore _streamStore;
        readonly IEventSerializer _serializer;
        const int ChunkSize = 500;

        /// <summary>
        /// Create a new SqlStreamStore producer instance
        /// </summary>
        /// <param name="streamStore">IStreamStore instance</param>
        /// <param name="serializer">Event serializer instance</param>
        public SqlStreamStoreProducer(IStreamStore streamStore, IEventSerializer serializer) {
            _streamStore = Ensure.NotNull(streamStore, nameof(streamStore));
            _serializer = Ensure.NotNull(serializer, nameof(serializer));
        }

        /// <summary>
        /// Create a new SqlStreamStore producer instance with an MSSQL (e.g. Azure Sql) store
        /// </summary>
        /// <param name="msSqlSettings">settings for creating an MSSQL based SqlStreamStore instance</param>
        /// <param name="serializer">Event serializer instance</param>
        public SqlStreamStoreProducer(MsSqlStreamStoreV3Settings msSqlSettings, IEventSerializer serializer) 
            : this(new MsSqlStreamStoreV3(Ensure.NotNull(msSqlSettings, nameof(msSqlSettings))), serializer) { }
        
        /// <summary>
        /// Create a new SqlStreamStore producer instance with an MySQL store
        /// </summary>
        /// <param name="mySqlSettings">settings for creating a MySql based SqlStreamStore instance</param>
        /// <param name="serializer">Event serializer instance</param>
        public SqlStreamStoreProducer(MySqlStreamStoreSettings mySqlSettings, IEventSerializer serializer) 
            : this(new MySqlStreamStore(Ensure.NotNull(mySqlSettings, nameof(mySqlSettings))), serializer) { }


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
                await _streamStore.AppendToStream(
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

            return _streamStore.AppendToStream(
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
