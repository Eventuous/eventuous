// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using static Eventuous.RabbitMq.Subscriptions.RabbitMqSubscription;

namespace Eventuous.RabbitMq.Subscriptions;

/// <summary>
/// Options for a RabbitMQ subscription
/// </summary>
[PublicAPI]
public record RabbitMqSubscriptionOptions : SubscriptionOptions {
    /// <summary>
    /// Exchange for where the subscription binds to.
    /// </summary>
    public string Exchange { get; set; } = null!;

    /// <summary>
    /// Queue name for the subscription. If it's null, the subscription id will be used as the queue name.
    /// </summary>
    public string? Queue { get; set; }

    /// <summary>
    /// Alternative way to deal with failed messages. The default strategy is to requeue.
    /// </summary>
    public HandleEventProcessingFailure? FailureHandler { get; set; }

    /// <summary>
    /// RabbitMQ exchange options (optional).
    /// </summary>
    public RabbitMqExchangeOptions? ExchangeOptions { get; set; } = new();

    /// <summary>
    /// RabbitMQ queue options (optional).
    /// </summary>
    public RabbitMqQueueOptions? QueueOptions { get; set; } = new();

    /// <summary>
    /// RabbitMQ queue to exchange binding options (optional).
    /// </summary>
    public RabbitMqBindingOptions? BindingOptions { get; set; } = new();

    /// <summary>
    /// The number of consumers running in parallel. Default is 1.
    /// </summary>
    public uint ConcurrencyLimit { get; set; } = 1;

    /// <summary>
    /// Prefetch count. Will be adjusted automatically if not set.
    /// </summary>
    public ushort PrefetchCount { get; set; }

    /// <summary>
    /// RabbitMQ exchange options.
    /// </summary>
    [PublicAPI]
    public record RabbitMqExchangeOptions {
        /// <summary>
        /// Exchange type, default is fan out.
        /// </summary>
        public string Type { get; set; } = ExchangeType.Fanout;

        /// <summary>
        /// Should the exchange be deleted when the service shuts down. Default is false.
        /// </summary>
        public bool AutoDelete { get; set; }

        /// <summary>
        /// Should the exchange be durable or not. Default is true.
        /// </summary>
        public bool Durable { get; set; } = true;

        /// <summary>
        /// Exchange arguments, passed directly to RMQ client.
        /// </summary>
        public IDictionary<string, object>? Arguments { get; set; }
    }

    /// <summary>
    /// RabbitMQ queue options
    /// </summary>
    [PublicAPI]
    public record RabbitMqQueueOptions {
        /// <summary>
        /// Should the queue be durable. Default is true.
        /// </summary>
        public bool Durable { get; set; } = true;

        /// <summary>
        /// Should the queue be exclusive. Default is false.
        /// </summary>
        public bool Exclusive { get; set; }

        /// <summary>
        /// Should the queue be deleted when the service shuts down. Default is false.
        /// </summary>
        public bool AutoDelete { get; set; }

        /// <summary>
        /// Queue arguments, passed directly to RMQ client
        /// </summary>
        public IDictionary<string, object>? Arguments { get; set; }
    }

    /// <summary>
    /// RabbitMQ queue to exchange binding options
    /// </summary>
    [PublicAPI]
    public record RabbitMqBindingOptions {
        /// <summary>
        /// Routing key for the binding. Learn more in RMQ documentation.
        /// </summary>
        public string? RoutingKey { get; set; }

        /// <summary>
        /// Binding arguments. Passed directly to RMQ client.
        /// </summary>
        public IDictionary<string, object>? Arguments { get; set; }
    }
}