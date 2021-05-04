using System;
using EventStore.Client;

namespace Eventuous.Subscriptions.EventStoreDB {
    public class EventStoreSubscriptionOptions {
        /// <summary>
        /// Optional function to configure client operation options
        /// </summary>
        public Action<EventStoreClientOperationOptions>? ConfigureOperation { get; init; }
        
        /// <summary>
        /// User credentials
        /// </summary>
        public UserCredentials? Credentials { get; init; }

        /// <summary>
        /// Resolve link events
        /// </summary>
        public bool ResolveLinkTos { get; init; } = false;
    }
}