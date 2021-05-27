using System;
using System.Threading.Tasks;
using System.Threading;
using Eventuous.Producers.SqlStreamStore;
using JetBrains.Annotations;
using SqlStreamStore;
using SqlStreamStore.Streams;

namespace Eventuous.Producers.SqlStreamStore.MsSql
{
    /// <summary>
    /// Producer for SqlStreamStore (https://sqlstreamstore.readthedocs.io) which has a MSSQL database as the event data store.
    /// </summary>
    [PublicAPI]
    public class MsSqlStreamStoreProducer : SqlStreamStoreProducer
    {
        public MsSqlStreamStoreProducer(MsSqlStreamStoreV3 msSqlStore, IEventSerializer serializer) 
            : base(Ensure.NotNull(msSqlStore, nameof(msSqlStore)), serializer) { }

        /// <summary>
        /// Create a new SqlStreamStore producer instance with an MSSQL (e.g. Azure Sql) store
        /// </summary>
        /// <param name="msSqlSettings">settings for creating an MSSQL based SqlStreamStore instance</param>
        /// <param name="serializer">Event serializer instance</param>
        public MsSqlStreamStoreProducer(MsSqlStreamStoreV3Settings msSqlSettings, IEventSerializer serializer) 
            : base(new MsSqlStreamStoreV3(Ensure.NotNull(msSqlSettings, nameof(msSqlSettings))), serializer) { }

        /// <summary>
        /// Initializes the database, like creating the schema for storing the events.
        /// </summary>
        public override Task Initialize(CancellationToken cancellationToken = default) 
            => ((MsSqlStreamStoreV3) StreamStore).CreateSchemaIfNotExists();

    }
}
