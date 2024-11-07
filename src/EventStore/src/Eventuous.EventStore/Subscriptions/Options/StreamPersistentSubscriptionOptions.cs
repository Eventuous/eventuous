// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

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