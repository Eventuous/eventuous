using System.Collections.Generic;
using JetBrains.Annotations;

namespace Eventuous.Producers.RabbitMq {
    [PublicAPI]
    public class RabbitMqProduceOptions {
        public string? AppId         { get; init; }
        public byte    DeliveryMode  { get; init; } = DefaultDeliveryMode;
        public string? CorrelationId { get; init; }
        public string? Expiration    { get; init; }
        public string? MessageId     { get; init; }
        public byte    Priority      { get; init; }
        public string? ReplyTo       { get; init; }
        public bool    Persisted     { get; init; } = true;

        public IDictionary<string, object>? Headers { get; init; }

        internal const byte DefaultDeliveryMode = 2;
    }
}