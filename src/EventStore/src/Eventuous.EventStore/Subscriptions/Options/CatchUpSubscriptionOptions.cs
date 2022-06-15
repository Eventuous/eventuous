namespace Eventuous.EventStore.Subscriptions;

public record CatchUpSubscriptionOptions : EventStoreSubscriptionOptions {
    public int ConcurrencyLimit { get; set; } = 1;
}
