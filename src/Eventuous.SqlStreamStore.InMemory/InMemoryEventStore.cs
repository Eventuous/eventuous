using System;
using Eventuous.SqlStreamStore;
using SqlStreamStore;

namespace Eventuous.SqlStreamStore.InMemory
{
    public class InMemoryEventStore : SqlEventStore
    {
        public InMemoryEventStore(InMemoryStreamStore inMemoryStore) : base(inMemoryStore) {}
    }
}
