// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions;

[PublicAPI]
public abstract record SubscriptionOptions {
    /// <summary>
    /// Subscription id is used to match event handlers with one subscription
    /// </summary>
    public string SubscriptionId { get; set; } = null!;

    /// <summary>
    /// Set to true if you want the subscription to fail and stop if anything goes wrong.
    /// </summary>
    public bool ThrowOnError { get; set; }
}

public abstract record SubscriptionWithCheckpointOptions : SubscriptionOptions {
    public int CheckpointCommitBatchSize { get; set; } = 100;
    public int CheckpointCommitDelayMs   { get; set; } = 5000;
}
