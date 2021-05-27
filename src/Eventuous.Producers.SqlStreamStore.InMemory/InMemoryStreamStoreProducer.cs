using System;
using Eventuous.Producers.SqlStreamStore;
using JetBrains.Annotations;
using SqlStreamStore;

namespace Eventuous.Producers.SqlStreamStore.InMemory
{
    /// <summary>
    /// Producer for SqlStreamStore (https://sqlstreamstore.readthedocs.io) which has an in-memory database as the event data store.
    /// </summary>
    [PublicAPI]
    public class InMemoryStreamStoreProducer : SqlStreamStoreProducer
    {
        /// <summary>
        /// Create an in-memory event data store
        /// </summary>
        /// <param name="serializer">Event serializer instance</param>
        public InMemoryStreamStoreProducer(InMemoryStreamStore store, IEventSerializer serializer) 
            : base(Ensure.NotNull(store, nameof(store)), serializer) { }

    }
}
