namespace Eventuous.EventStore.Subscriptions;

[PublicAPI]
public abstract record PersistentSubscriptionOptions : EventStoreSubscriptionOptions {
    /// <summary>
    /// Native EventStoreDB settings for the subscription
    /// </summary>
    public PersistentSubscriptionSettings? SubscriptionSettings { get; set; }

    /// <summary>
    /// Size of the subscription buffer
    /// </summary>
    public int BufferSize { get; set; } = 10;

    public TimeSpan? Deadline { get; set; }

    // public uint ConcurrencyLevel { get; set; } = 1;

    /// <summary>
    /// Allows to override the failure handling behaviour. By default, when the consumer crashes, the event is
    /// retries and then NACKed. You can use this function to, for example, park the failed event.
    /// </summary>
    public HandleEventProcessingFailure? FailureHandler { get; set; }
}