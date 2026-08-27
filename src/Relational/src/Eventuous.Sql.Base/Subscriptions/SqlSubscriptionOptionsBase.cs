// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;

namespace Eventuous.Sql.Base.Subscriptions;

/// <summary>
/// Options for SQL-based subscriptions.
/// </summary>
public abstract record SqlSubscriptionOptionsBase : SubscriptionWithCheckpointOptions {
    /// <summary>
    /// Database schema name. Default is "eventuous".
    /// </summary>
    public string Schema { get; set; } = "eventuous";
    /// <summary>
    /// Define the number of consumers than process messages concurrently. Default is 1.
    /// </summary>
    public int ConcurrencyLimit { get; set; } = 1;
    /// <summary>
    /// How many messages to fetch at once. Default is 1024.
    /// </summary>
    public int MaxPageSize { get; set; } = 1024;
    /// <summary>
    /// Polling query options.
    /// </summary>
    public PollingOptions Polling { get; set; } = new();
    /// <summary>
    /// Retry options.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Gap age threshold in milliseconds. If != null, a gap is ignored entirely when the event that follows it is
    /// older than this threshold, so replaying history doesn't wait for transactions that finished long ago.
    /// It's also what lets a subscription eventually move past a position no transaction will ever fill.
    /// Default is 1 hour.
    /// </summary>
    public int? GapAgeThresholdMs { get; set; } = 60 * 60 * 1000;

    /// <summary>
    /// Gap skip timeout in milliseconds. If != null, a detected gap stops holding the subscription after this
    /// duration, and the subscription advances past the missing position. The position is abandoned on elapsed
    /// time alone, so an append taking longer than this to commit will have its event skipped. Prefer
    /// <see cref="GapHandlingTimeoutMs"/>, which resolves a gap without that risk and takes precedence over this
    /// timeout; set this only when advancing matters more than never missing an event.
    /// Default is null (never abandon a position on time alone).
    /// </summary>
    public int? GapSkipTimeoutMs { get; set; }

    /// <summary>
    /// Gap handling timeout in milliseconds. If != null, when a gap in the global position sequence is detected
    /// and it persists for at least this duration, the subscription will attempt to handle it in a provider-specific
    /// way (e.g. creating tombstones). Unlike <see cref="GapSkipTimeoutMs"/>, this resolves the position rather than
    /// abandoning it, and so runs before any skip. Default is null (don't create tombstones).
    /// </summary>
    public int? GapHandlingTimeoutMs { get; set; }

    /// <summary>
    /// Options for polling.
    /// </summary>
    public record PollingOptions {
        /// <summary>
        /// Minimum interval between polling attempts in milliseconds. Default is 5.
        /// </summary>
        public int MinIntervalMs { get; set; } = 5;

        /// <summary>
        /// Maximum interval between polling attempts in milliseconds. Default is 1000.
        /// </summary>
        public int MaxIntervalMs { get; set; } = 1000;
        /// <summary>
        /// How much to grow the interval between polling attempts. Default is 1.5.
        /// </summary>
        public double GrowFactor { get; set; } = 1.5;
    }

    /// <summary>
    /// Retry options.
    /// </summary>
    public record RetryOptions {
        /// <summary>
        /// Initial delay between retries in milliseconds. Default is 50.
        /// </summary>
        public int InitialDelayMs { get; set; } = 50;
    }
}
