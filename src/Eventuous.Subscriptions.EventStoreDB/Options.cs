using System;
using EventStore.Client;
using JetBrains.Annotations;

namespace Eventuous.Subscriptions.EventStoreDB {
    public abstract class EventStoreSubscriptionOptions : SubscriptionOptions {
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
        public bool ResolveLinkTos { get; init; }
    }

    public class StreamSubscriptionOptions : EventStoreSubscriptionOptions {
        public string StreamName { get; init; } = null!;
    }

    public class AllStreamSubscriptionOptions : EventStoreSubscriptionOptions { }

    [PublicAPI]
    public class StreamPersistentSubscriptionOptions : EventStoreSubscriptionOptions {
        public string Stream { get; init; } = null!;

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

        public StreamPersistentSubscription.HandleEventProcessingFailure? FailureHandler { get; init; }
    }
}