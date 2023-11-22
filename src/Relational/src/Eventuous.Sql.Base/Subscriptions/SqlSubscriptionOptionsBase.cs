// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;

namespace Eventuous.Sql.Base.Subscriptions;

public abstract record SqlSubscriptionOptionsBase : SubscriptionWithCheckpointOptions {
    public string         Schema           { get; set; } = "eventuous";
    public int            ConcurrencyLimit { get; set; } = 1;
    public int            MaxPageSize      { get; set; } = 1024;
    public PollingOptions Polling          { get; set; } = new();
    public RetryOptions   Retry            { get; set; } = new();

    public record PollingOptions {
        public int    MinIntervalMs { get; set; } = 5;
        public int    MaxIntervalMs { get; set; } = 1000;
        public double GrowFactor    { get; set; } = 1.5;
    }

    public record RetryOptions {
        public int    InitialDelayMs  { get; set; } = 50;
    }
}
