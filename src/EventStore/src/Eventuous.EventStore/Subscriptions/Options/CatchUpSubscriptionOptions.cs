namespace Eventuous.EventStore.Subscriptions;

public record CatchUpSubscriptionOptions : EventStoreSubscriptionOptions {
    public uint ConcurrencyLimit { get; set; } = 1;
}
