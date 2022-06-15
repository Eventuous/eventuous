// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using static Eventuous.RabbitMq.Subscriptions.RabbitMqSubscription;

namespace Eventuous.RabbitMq.Subscriptions;

[PublicAPI]
public record RabbitMqSubscriptionOptions : SubscriptionOptions {
    public string                        Exchange        { get; set; } = null!;
    public HandleEventProcessingFailure? FailureHandler  { get; set; }
    public RabbitMqExchangeOptions?      ExchangeOptions { get; set; } = new();
    public RabbitMqQueueOptions?         QueueOptions    { get; set; } = new();
    public RabbitMqBindingOptions?       BindingOptions  { get; set; } = new();

    public uint   ConcurrencyLimit { get; set; } = 1;
    public ushort PrefetchCount    { get; set; }

    [PublicAPI]
    public record RabbitMqExchangeOptions {
        public string Type       { get; set; } = ExchangeType.Fanout;
        public bool   AutoDelete { get; set; }
        public bool   Durable    { get; set; } = true;

        public IDictionary<string, object>? Arguments { get; set; }
    }

    [PublicAPI]
    public record RabbitMqQueueOptions {
        public bool Durable    { get; set; } = true;
        public bool Exclusive  { get; set; }
        public bool AutoDelete { get; set; }

        public IDictionary<string, object>? Arguments { get; set; }
    }

    [PublicAPI]
    public record RabbitMqBindingOptions {
        public string? RoutingKey { get; set; }

        public IDictionary<string, object>? Arguments { get; set; }
    }
}