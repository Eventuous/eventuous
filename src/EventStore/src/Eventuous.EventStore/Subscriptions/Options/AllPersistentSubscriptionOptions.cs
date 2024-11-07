// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Options for <see cref="AllPersistentSubscription"/>
/// </summary>
public record AllPersistentSubscriptionOptions : PersistentSubscriptionOptions {
    /// <summary>
    /// Server-side event filter.
    /// Warning: the filter is set when the subscription is created.
    /// Eventuous doesn't update the filter after that even if it changes in code.
    /// </summary>
    public IEventFilter? EventFilter { get; set; }
}