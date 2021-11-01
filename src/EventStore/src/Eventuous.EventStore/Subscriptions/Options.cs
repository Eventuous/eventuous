namespace Eventuous.EventStore.Subscriptions;

[PublicAPI]
public abstract record EventStoreSubscriptionOptions : SubscriptionOptions {
    /// <summary>
    /// Optional function to configure client operation options
    /// </summary>
    public Action<EventStoreClientOperationOptions>? ConfigureOperation { get; set; }

    /// <summary>
    /// User credentials
    /// </summary>
    public UserCredentials? Credentials { get; set; }

    /// <summary>
    /// Resolve link events
    /// </summary>
    public bool ResolveLinkTos { get; set; }

    /// <summary>
    /// Metadata serializer. If not assigned, the default one will be used.
    /// </summary>
    public IMetadataSerializer? MetadataSerializer { get; set; }
}

public record StreamSubscriptionOptions : EventStoreSubscriptionOptions {
    public string StreamName { get; set; } = null!;
}

[PublicAPI]
public record AllStreamSubscriptionOptions : EventStoreSubscriptionOptions {
    public IEventFilter? EventFilter        { get; set; }
    public uint          CheckpointInterval { get; set; } = 10;
}

[PublicAPI]
public record StreamPersistentSubscriptionOptions : EventStoreSubscriptionOptions {
    public string Stream { get; set; } = null!;

    /// <summary>
    /// Detailed settings for the subscription
    /// </summary>
    public PersistentSubscriptionSettings? SubscriptionSettings { get; set; }

    /// <summary>
    /// Size of the subscription buffer
    /// </summary>
    public int BufferSize { get; set; } = 10;

    /// <summary>
    /// Acknowledge events without an explicit ACK
    /// </summary>
    public bool AutoAck { get; set; } = true;

    public StreamPersistentSubscription.HandleEventProcessingFailure? FailureHandler { get; set; }
}