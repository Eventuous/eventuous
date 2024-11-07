// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Options base record for EventStoreDB checkpoint-based subscriptions
/// </summary>
public abstract record EventStoreSubscriptionWithCheckpointOptions : SubscriptionWithCheckpointOptions {
    /// <summary>
    /// User credentials
    /// </summary>
    public UserCredentials? Credentials { get; [PublicAPI] set; }

    /// <summary>
    /// Resolve link events
    /// </summary>
    public bool ResolveLinkTos { get; set; }
}
