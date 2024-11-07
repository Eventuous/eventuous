// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.RabbitMq.Shared;
using Eventuous.Subscriptions;

namespace Eventuous.RabbitMq.Subscriptions;

using static RabbitMqSubscription;

/// <summary>
/// Options for RabbitMQ subscription.
/// </summary>
[PublicAPI]
public record RabbitMqSubscriptionOptions : SubscriptionOptions {
    /// <summary>
    /// Exchange name to subscribe to.
    /// </summary>
    public string Exchange { get; set; } = null!;

    /// <summary>
    /// A function to handle event processing failure. If not specified, the default handler will be used.
    /// </summary>
    public HandleEventProcessingFailure? FailureHandler { get; set; }

    /// <summary>
    /// Optional options for the RabbitMQ exchange.
    /// </summary>
    public RabbitMqExchangeOptions ExchangeOptions { get; set; } = new();

    /// <summary>
    /// Optional options for the RabbitMQ queue.
    /// </summary>
    public RabbitMqQueueOptions QueueOptions { get; set; } = new();

    /// <summary>
    /// Optional options for the RabbitMQ binding.
    /// </summary>
    public RabbitMqBindingOptions BindingOptions { get; set; } = new();

    /// <summary>
    /// Number of concurrent consumers, default is one.
    /// </summary>
    public uint ConcurrencyLimit { get; set; } = 1;

    /// <summary>
    /// Number of messages to prefetch.
    /// </summary>
    public ushort PrefetchCount { get; set; }

    [PublicAPI]
    public record RabbitMqQueueOptions {
        public string? Queue      { get; set; }
        public bool    Durable    { get; set; } = true;
        public bool    Exclusive  { get; set; }
        public bool    AutoDelete { get; set; }

        public IDictionary<string, object>? Arguments { get; set; }
    }

    [PublicAPI]
    public record RabbitMqBindingOptions {
        public string RoutingKey { get; set; } = "";

        public IDictionary<string, object>? Arguments { get; set; }
    }
}
