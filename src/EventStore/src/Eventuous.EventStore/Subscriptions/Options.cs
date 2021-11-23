namespace Eventuous.EventStore.Subscriptions;

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
    /// <summary>
    /// WHen set to true, all events of type that starts with '$' will be ignored. Default is true.
    /// </summary>
    public bool IgnoreSystemEvents { get; set; } = true;

    /// <summary>
    /// Stream name to subscribe for
    /// </summary>
    public StreamName StreamName { get; set; } = null!;
}

[PublicAPI]
public record AllStreamSubscriptionOptions : EventStoreSubscriptionOptions {
    /// <summary>
    /// Server-side event filter
    /// </summary>
    public IEventFilter? EventFilter { get; set; }

    /// <summary>
    /// When using the server-side <see cref="EventFilter"/>, the clients still wants to persist the checkpoint
    /// from time to time, to avoid re-reading lots of filtered out events after the restart. Default is 10.
    /// </summary>
    public uint CheckpointInterval { get; set; } = 10;
}

[PublicAPI]
public record StreamPersistentSubscriptionOptions : EventStoreSubscriptionOptions {
    /// <summary>
    /// Stream name to subscribe for
    /// </summary>
    public StreamName StreamName { get; set; } = null!;

    /// <summary>
    /// Native EventStoreDB settings for the subscription
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

    // public uint ConcurrencyLevel { get; set; } = 1;

    /// <summary>
    /// Allows to override the failure handling behaviour. By default, when the consumer crashes, the event is
    /// retries and then NACKed. You can use this function to, for example, park the failed event.
    /// </summary>
    public StreamPersistentSubscription.HandleEventProcessingFailure? FailureHandler { get; set; }
}