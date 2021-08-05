using System;
using EventStore.Client;

namespace Eventuous.EventStoreDB.Producers {
    public class EventStoreProduceOptions {
        /// <summary>
        /// Message metadata
        /// </summary>
        public object? Metadata { get; init; }
        
        /// <summary>
        /// User credentials
        /// </summary>
        public UserCredentials? Credentials { get; init; }
        
        /// <summary>
        /// Expected stream state
        /// </summary>
        public StreamState ExpectedState { get; init; } = StreamState.Any;

        /// <summary>
        /// Optional function to configure client operation options
        /// </summary>
        public Action<EventStoreClientOperationOptions>? ConfigureOperation { get; init; }
    }
}