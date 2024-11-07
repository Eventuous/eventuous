// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Filters;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Base class for catch-up subscription options
/// </summary>
public record CatchUpSubscriptionOptions : EventStoreSubscriptionWithCheckpointOptions {
    /// <summary>
    /// Number of parallel consumers. Defaults to 1.
    /// Don't set this value if you use partitioned subscriptions with <see cref="PartitioningFilter"/>.
    /// </summary>
    public int ConcurrencyLimit { get; set; } = 1;
}
