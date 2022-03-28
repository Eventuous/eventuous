namespace Eventuous.EventStore.Subscriptions;

[PublicAPI]
public record StreamPersistentSubscriptionOptions : PersistentSubscriptionOptions {
    /// <summary>
    /// Stream name to subscribe for
    /// </summary>
    public StreamName StreamName { get; set; }
}