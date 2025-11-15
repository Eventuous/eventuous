// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.KurrentDB.Subscriptions;

/// <summary>
/// Base class for EventStoreDB subscription options
/// </summary>
public abstract record KurrentDBSubscriptionOptions : SubscriptionOptions {
    /// <summary>
    /// User credentials
    /// </summary>
    public UserCredentials? Credentials { get; [PublicAPI] set; }

    /// <summary>
    /// Resolve link events
    /// </summary>
    public bool ResolveLinkTos { get; [PublicAPI] set; }
}
