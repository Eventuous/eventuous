// Copyright (C) Eventuous HQ OÜ.All rights reserved
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
