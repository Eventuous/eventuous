using System.Collections.Generic;
using JetBrains.Annotations;
using RabbitMQ.Client;

namespace Eventuous.RabbitMq.Producers {
    [PublicAPI]
    public class RabbitMqExchangeOptions {
        public string Type       { get; init; } = ExchangeType.Fanout;
        public bool   Durable    { get; init; } = true;
        public bool   AutoDelete { get; init; }

        public IDictionary<string, object>? Arguments { get; init; }
    }
}