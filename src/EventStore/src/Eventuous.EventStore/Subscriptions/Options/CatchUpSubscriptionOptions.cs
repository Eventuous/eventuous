using Eventuous.Subscriptions.Filters;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Base class for catch-up subscription options
/// </summary>
public record CatchUpSubscriptionOptions : EventStoreSubscriptionOptions {
    /// <summary>
    /// Number of parallel consumers. Defaults to 1.
    /// Don't set this value if you use partitioned subscriptions with <see cref="PartitioningFilter"/>.
    /// </summary>
    public int ConcurrencyLimit { get; set; } = 1;
}
