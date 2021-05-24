using System;
using SqlStreamStore.Streams;

namespace Eventuous.Producers.SqlStreamStore {
    public class SqlStreamStoreProduceOptions {
        
        /// <summary>
        /// Message metadata
        /// </summary>
        public object? Metadata { get; init; }
        /// <summary>
        /// Expected stream state
        /// </summary>
        public int ExpectedState { get; init; } = ExpectedVersion.Any;

    }
}