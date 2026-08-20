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

    /// <summary>
    /// How long the subscription waits before replacing a dropped connection. Default is two seconds.
    /// </summary>
    /// <remarks>
    /// Sets the load an unreachable broker sees from a fleet of retrying instances, and how long a recovered
    /// one takes to be noticed.
    /// </remarks>
    public TimeSpan RetryDelay { get; set; } = DefaultRetryDelay;

    /// <summary>
    /// Default for <see cref="RetryDelay"/>, and its fallback when set to a delay that can't be waited on.
    /// </summary>
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long a resubscribe lets the transport take over releasing its connection, joining its message loop
    /// and closing out the run it is replacing, before it is asked to stop being graceful about it. Default is
    /// five seconds.
    /// </summary>
    /// <remarks>
    /// Not a deadline: teardown waits for every release regardless. Once it elapses releases are asked to drop
    /// what's optional, but essential work — the final checkpoint flush above all — still completes.
    /// </remarks>
    public TimeSpan TeardownTimeout { get; set; } = DefaultTeardownTimeout;

    /// <summary>
    /// Default for <see cref="TeardownTimeout"/>, and its fallback when set to a value that can't be waited on.
    /// </summary>
    public static readonly TimeSpan DefaultTeardownTimeout = TimeSpan.FromSeconds(5);
}

public abstract record SubscriptionWithCheckpointOptions : SubscriptionOptions {
    /// <summary>
    /// Checkpoint will be committed after processing this number of events. Default is 100.
    /// The <seealso cref="CheckpointCommitDelayMs"/> option will be considered as well, so the commit will happen when either condition is met.
    /// </summary>
    public int             CheckpointCommitBatchSize { get; set; } = 100;

    /// <summary>
    /// Checkpoint will be committed after this delay. Default is 5 seconds.
    /// The <seealso cref="CheckpointCommitBatchSize"/> option will be considered as well, so the commit will happen when either condition is met.
    /// </summary>
    public int             CheckpointCommitDelayMs   { get; set; } = 5000;

    /// <summary>
    /// Where to start reading events from if there's no checkpoint. Default is from the beginning.
    /// </summary>
    public InitialPosition StartFrom                 { get; set; } = InitialPosition.Earliest;
}
