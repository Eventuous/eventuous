using System.Collections.Generic;
using Eventuous.Subscriptions;
using JetBrains.Annotations;
using RabbitMQ.Client;
using static Eventuous.RabbitMq.Subscriptions.RabbitMqSubscriptionService;

namespace Eventuous.RabbitMq.Subscriptions {
    [PublicAPI]
    public class RabbitMqSubscriptionOptions : SubscriptionOptions {
        public string                        SubscriptionQueue { get; init; } = null!;
        public string                        Exchange          { get; init; } = null!;
        public HandleEventProcessingFailure? FailureHandler    { get; init; }
        public RabbitMqExchangeOptions?      ExchangeOptions   { get; init; } = new();
        public RabbitMqQueueOptions?         QueueOptions      { get; init; } = new();
        public RabbitMqBindingOptions?       BindingOptions    { get; init; } = new();

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