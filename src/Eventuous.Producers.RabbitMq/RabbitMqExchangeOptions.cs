using System.Collections.Generic;
using RabbitMQ.Client;

namespace Eventuous.Producers.RabbitMq {
    public class RabbitMqExchangeOptions {
        public string Type       { get; init; } = ExchangeType.Fanout;
        public bool   Durable    { get; init; } = true;
        public bool   AutoDelete { get; init; } = false;

        public IDictionary<string, object>? Arguments { get; init; }
    }
}