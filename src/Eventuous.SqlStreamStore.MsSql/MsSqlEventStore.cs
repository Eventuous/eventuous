using System;
using Eventuous.SqlStreamStore;
using SqlStreamStore;

namespace Eventuous.SqlStreamStore.MsSql
{
    public class MsSqlEventStore : SqlEventStore
    {
        public MsSqlEventStore(MsSqlStreamStoreV3 msSqlStore) : base(msSqlStore) {}
    }
}
