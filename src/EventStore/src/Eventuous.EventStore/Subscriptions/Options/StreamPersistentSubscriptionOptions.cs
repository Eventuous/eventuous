namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Options for <see cref="StreamPersistentSubscription"/>
/// </summary>
[PublicAPI]
public record StreamPersistentSubscriptionOptions : PersistentSubscriptionOptions {
    /// <summary>
    /// Stream name to subscribe for
    /// </summary>
    public StreamName StreamName { get; set; }
}