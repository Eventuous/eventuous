# Eventuous Kafka Integration

NuGet package: `Eventuous.Kafka`
Namespace: `Eventuous.Kafka.Producers`, `Eventuous.Kafka.Subscriptions`
Source: `src/Kafka/src/Eventuous.Kafka/`

Uses Confluent.Kafka under the hood. Produces byte[] payloads with type info in Kafka headers (no schema registry).

## Producer

`KafkaBasicProducer` extends `BaseProducer<KafkaProduceOptions>` and implements `IHostedProducer`, `IAsyncDisposable`.

```csharp
// Constructor takes KafkaProducerOptions (wraps Confluent ProducerConfig)
public record KafkaProducerOptions(ProducerConfig ProducerConfig);

// Per-message produce options (partition key)
public record KafkaProduceOptions(string PartitionKey);
```

### Instantiation

```csharp
var options = new KafkaProducerOptions(new ProducerConfig {
    BootstrapServers = "localhost:9092"
});
await using var producer = new KafkaBasicProducer(options);
await producer.StartAsync(cancellationToken);

// Produce with partition key
await producer.Produce(
    new StreamName("my-topic"),
    events,
    new Metadata(),
    new KafkaProduceOptions("my-partition-key"),
    cancellationToken: ct
);
```

### DI Registration

```csharp
services.AddProducer<KafkaBasicProducer>(sp =>
    new KafkaBasicProducer(
        new KafkaProducerOptions(new ProducerConfig {
            BootstrapServers = "localhost:9092"
        })
    )
);
```

### How it works

- Serializes events using `IEventSerializer`, sends as `byte[]` values
- Stores event type in `message-type` header and content type in `content-type` header (configurable via `KafkaHeaderKeys`)
- When `PartitionKey` is provided, uses a keyed producer (`IProducer<string, byte[]>`); otherwise uses `IProducer<Null, byte[]>`
- Metadata entries are converted to Kafka headers via `MetadataExtensions.AsKafkaHeaders()`
- Supports delivery acknowledgement callbacks (`OnAck`/`OnFail`)
- `StopAsync` flushes pending messages before stopping

### Header Keys

```csharp
public static class KafkaHeaderKeys {
    public static string MessageTypeHeader { get; set; } = "message-type";
    public static string ContentTypeHeader { get; set; } = "content-type";
}
```

## Subscription

`KafkaBasicSubscription` extends `EventSubscription<KafkaSubscriptionOptions>`. Note: the subscription is currently a stub (throws `NotImplementedException`).

```csharp
public record KafkaSubscriptionOptions : SubscriptionOptions {
    public ConsumerConfig ConsumerConfig { get; init; } = null!;
}
```

## Tracing

Built-in OpenTelemetry tracing with:
- `MessagingSystem = "kafka"`
- `DestinationKind = "topic"`
- `ProduceOperation = "produce"`
