namespace Eventuous.EventStore.Subscriptions; 

public abstract record EventStoreSubscriptionOptions : SubscriptionOptions {
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
    
    /// <summary>
    /// Metadata serializer. If not assigned, the default one will be used.
    /// </summary>
    public IMetadataSerializer? MetadataSerializer { get; init; }
}

public record StreamSubscriptionOptions : EventStoreSubscriptionOptions {
    public string StreamName { get; init; } = null!;
}

public record AllStreamSubscriptionOptions : EventStoreSubscriptionOptions {
    public IEventFilter? EventFilter { get; init; }
}

[PublicAPI]
public record StreamPersistentSubscriptionOptions : EventStoreSubscriptionOptions {
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