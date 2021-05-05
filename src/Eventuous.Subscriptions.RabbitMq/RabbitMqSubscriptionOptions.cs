using System.Collections.Generic;
using JetBrains.Annotations;
using RabbitMQ.Client;

namespace Eventuous.Subscriptions.RabbitMq {
    [PublicAPI]
    public class RabbitMqSubscriptionOptions {
        public RabbitMqExchangeOptions? ExchangeOptions { get; init; } = new();
        public RabbitMqQueueOptions?    QueueOptions    { get; init; } = new();
        public RabbitMqBindingOptions?  BindingOptions  { get; init; } = new();

        public int ConcurrencyLimit { get; init; } = 1;

        [PublicAPI]
        public class RabbitMqExchangeOptions {
            public string Type       { get; init; } = ExchangeType.Fanout;
            public bool   AutoDelete { get; init; }
            public bool   Durable    { get; init; } = true;

            public IDictionary<string, object>? Arguments { get; init; }
        }

        [PublicAPI]
        public class RabbitMqQueueOptions {
            public bool Durable    { get; init; } = true;
            public bool Exclusive  { get; init; }
            public bool AutoDelete { get; init; }

            public IDictionary<string, object>? Arguments { get; init; }
        }

        [PublicAPI]
        public class RabbitMqBindingOptions {
            public string? RoutingKey { get; init; }

            public IDictionary<string, object>? Arguments { get; init; }
        }
    }
}