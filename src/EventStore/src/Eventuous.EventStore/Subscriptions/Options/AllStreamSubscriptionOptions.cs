// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Options for <see cref="AllStreamSubscription"/>
/// </summary>
[PublicAPI]
public record AllStreamSubscriptionOptions : CatchUpSubscriptionOptions {
    /// <summary>
    /// Server-side event filter
    /// </summary>
    public IEventFilter? EventFilter { get; set; }

    /// <summary>
    /// When using the server-side <see cref="EventFilter"/>, the clients still wants to persist the checkpoint
    /// from time to time, to avoid re-reading lots of filtered out events after the restart. Default is 10.
    /// </summary>
    public uint CheckpointInterval { get; set; } = 10;
}