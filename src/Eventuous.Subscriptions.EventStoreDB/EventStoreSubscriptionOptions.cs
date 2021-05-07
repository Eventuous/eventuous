using System;
using EventStore.Client;
using JetBrains.Annotations;

namespace Eventuous.Subscriptions.EventStoreDB {
    [PublicAPI]
    public class EventStoreSubscriptionOptions : SubscriptionOptions {
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