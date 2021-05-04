using EventStore.Client;
using JetBrains.Annotations;

namespace Eventuous.Subscriptions.EventStoreDB {
    [PublicAPI]
    public class EventStorePersistentSubscriptionOptions {
        /// <summary>
        /// User credentials
        /// </summary>
        public UserCredentials? Credentials { get; init; }
        
        /// <summary>
        /// Detailed settings for the subscription
        /// </summary>
        public PersistentSubscriptionSettings? SubscriptionSettings { get; init; }

        /// <summary>
        /// Size of the subscription buffer
        /// </summary>
        public int BufferSize { get; init; } = 10;

        /// <summary>
        /// Acknowledge events without an explicit ACK
        /// </summary>
        public bool AutoAck { get; init; } = true;
    }
}