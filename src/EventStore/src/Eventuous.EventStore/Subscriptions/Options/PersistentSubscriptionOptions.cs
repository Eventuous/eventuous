// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Base class for persistent subscription options
/// </summary>
[PublicAPI]
public abstract record PersistentSubscriptionOptions : EventStoreSubscriptionOptions {
    /// <summary>
    /// Native EventStoreDB settings for the subscription
    /// </summary>
    public PersistentSubscriptionSettings? SubscriptionSettings { get; set; }

    /// <summary>
    /// Size of the subscription buffer
    /// </summary>
    public int BufferSize { get; set; } = 10;

    /// <summary>
    /// Deadline for gRPC calls. Default is set by EventStoreDB client (10 sec).
    /// </summary>
    public TimeSpan? Deadline { get; set; }

    // public uint ConcurrencyLevel { get; set; } = 1;

    /// <summary>
    /// Allows overriding the failure handling behavior. By default, when the consumer crashes, the event is
    /// retries and then NACKed. You can use this function to, for example, park the failed event.
    /// </summary>
    public HandleEventProcessingFailure? FailureHandler { get; set; }
}