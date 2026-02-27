# Eventuous RabbitMQ Integration

## NuGet Package

```
Eventuous.RabbitMq
```

This package provides both producer and subscription implementations for RabbitMQ. It depends on the `RabbitMQ.Client` library.

## ConnectionFactory Setup

Both producer and subscription require a `RabbitMQ.Client.ConnectionFactory` registered as a singleton. You must set `DispatchConsumersAsync = true` for the subscription to work correctly.

```csharp
using RabbitMQ.Client;

// Do not hardcode credentials; use secret storage or environment variables
var connectionFactory = new ConnectionFactory {
    Uri                    = new(configuration["RabbitMq:ConnectionString"]!),
    DispatchConsumersAsync = true
};
services.AddSingleton(connectionFactory);
```

## RabbitMqProducer

**Namespace:** `Eventuous.RabbitMq.Producers`

`RabbitMqProducer` extends `BaseProducer<RabbitMqProduceOptions>` and implements `IHostedProducer`. It manages its own connection and channel, uses publisher confirms, and auto-declares exchanges on first publish.

### Constructor Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `connectionFactory` | `ConnectionFactory` | Yes | RabbitMQ connection factory |
| `serializer` | `IEventSerializer?` | No | Falls back to `DefaultEventSerializer.Instance` |
| `log` | `ILogger<RabbitMqProducer>?` | No | Logger |
| `options` | `RabbitMqExchangeOptions?` | No | Exchange configuration for auto-declared exchanges |

### RabbitMqExchangeOptions

**Namespace:** `Eventuous.RabbitMq.Shared`

Controls how the producer declares exchanges on first use.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Type` | `string` | `ExchangeType.Fanout` | Exchange type (fanout, direct, topic, headers) |
| `Durable` | `bool` | `true` | Survive broker restart |
| `AutoDelete` | `bool` | `false` | Delete when last consumer disconnects |
| `Arguments` | `IDictionary<string, object>?` | `null` | Additional exchange arguments |

### RabbitMqProduceOptions

Per-message options passed when producing.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RoutingKey` | `string?` | `null` | Routing key (for direct/topic exchanges) |
| `AppId` | `string?` | `null` | Application name for RabbitMQ management UI |
| `Expiration` | `int?` | `null` | Message TTL in milliseconds |
| `Priority` | `byte` | `0` | Message priority (0-9) |
| `ReplyTo` | `string?` | `null` | Reply address |
| `Persisted` | `bool` | `true` | Whether the message is persistent |

### Registration

```csharp
// Simple registration (resolves ConnectionFactory from DI)
services.AddProducer<RabbitMqProducer>();

// Factory registration with exchange options
services.AddProducer(sp => new RabbitMqProducer(
    sp.GetRequiredService<ConnectionFactory>(),
    options: new RabbitMqExchangeOptions {
        Type    = ExchangeType.Topic,
        Durable = true
    }
));
```

The producer implements `IHostedService` (via `IHostedProducer`), so `AddProducer` automatically registers it as a hosted service for connection lifecycle management.

### Producing Messages

The stream name passed to `Produce` is used as the RabbitMQ exchange name. The exchange is auto-declared on first publish.

```csharp
await producer.Produce(
    new StreamName("PaymentsIntegration"),
    myEvent,
    new Metadata(),
    new RabbitMqProduceOptions { RoutingKey = "payments.recorded" }
);
```

## RabbitMqSubscription

**Namespace:** `Eventuous.RabbitMq.Subscriptions`

`RabbitMqSubscription` extends `EventSubscription<RabbitMqSubscriptionOptions>`. It declares the exchange, queue, and binding on subscribe, then consumes with async event handling. Messages are acknowledged individually on success and rejected (with redelivery) on failure by default.

### RabbitMqSubscriptionOptions

A record extending `SubscriptionOptions`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SubscriptionId` | `string` | (required) | Unique subscription ID; also used as default queue name |
| `Exchange` | `string` | (required) | Exchange to consume from |
| `ThrowOnError` | `bool` | `false` | Stop subscription on processing error |
| `ConcurrencyLimit` | `uint` | `1` | Number of concurrent consumers |
| `PrefetchCount` | `ushort` | `0` | Prefetch count; if 0, defaults to `ConcurrencyLimit * 2` |
| `FailureHandler` | `HandleEventProcessingFailure?` | `null` | Custom failure handler delegate |
| `ExchangeOptions` | `RabbitMqExchangeOptions` | fanout, durable | Exchange declaration settings |
| `QueueOptions` | `RabbitMqQueueOptions` | durable | Queue declaration settings |
| `BindingOptions` | `RabbitMqBindingOptions` | `""` routing key | Queue-to-exchange binding settings |

### RabbitMqQueueOptions

Nested record inside `RabbitMqSubscriptionOptions`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Queue` | `string?` | `null` | Queue name; defaults to `SubscriptionId` if null |
| `Durable` | `bool` | `true` | Survive broker restart |
| `Exclusive` | `bool` | `false` | Exclusive to this connection |
| `AutoDelete` | `bool` | `false` | Delete when last consumer disconnects |
| `Arguments` | `IDictionary<string, object>?` | `null` | Additional queue arguments (e.g., dead-letter exchange) |

### RabbitMqBindingOptions

Nested record inside `RabbitMqSubscriptionOptions`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RoutingKey` | `string` | `""` | Binding routing key |
| `Arguments` | `IDictionary<string, object>?` | `null` | Additional binding arguments |

### Registration

```csharp
services.AddSubscription<RabbitMqSubscription, RabbitMqSubscriptionOptions>(
    "PaymentIntegration",
    builder => builder
        .Configure(x => {
            x.Exchange         = "PaymentsIntegration";
            x.ConcurrencyLimit = 2;
            x.ExchangeOptions  = new() { Type = ExchangeType.Fanout, Durable = true };
            x.QueueOptions     = new() { Durable = true };
            x.BindingOptions   = new() { RoutingKey = "" };
        })
        .AddEventHandler<MyEventHandler>()
);
```

No checkpoint store is needed for RabbitMQ subscriptions since RabbitMQ manages delivery tracking through its own acknowledgment mechanism.

## Gateway: Forwarding Events to RabbitMQ

The gateway pattern connects an event store subscription to a RabbitMQ producer for cross-context integration. Requires the `Eventuous.Gateway` package.

```csharp
using Eventuous.Gateway;
using Eventuous.RabbitMq.Producers;
using Eventuous.Postgresql.Subscriptions;

// Register the producer
services.AddProducer<RabbitMqProducer>();

// Register the gateway (subscription + producer + transform)
services.AddGateway<
    PostgresAllStreamSubscription,
    PostgresAllStreamSubscriptionOptions,
    RabbitMqProducer,
    RabbitMqProduceOptions>(
    "IntegrationSubscription",
    PaymentsGateway.Transform
);
```

The transform function maps consumed events to `GatewayMessage<RabbitMqProduceOptions>`:

```csharp
public static class PaymentsGateway {
    static readonly StreamName             Stream         = new("PaymentsIntegration");
    static readonly RabbitMqProduceOptions ProduceOptions = new();

    public static ValueTask<GatewayMessage<RabbitMqProduceOptions>[]> Transform(
        IMessageConsumeContext original
    ) {
        var result = original.Message is PaymentRecorded evt
            ? new GatewayMessage<RabbitMqProduceOptions>(
                Stream,
                new BookingPaymentRecorded(evt.BookingId, evt.Amount),
                new Metadata(),
                ProduceOptions
            )
            : null;

        return ValueTask.FromResult(result != null ? [result] : Array.Empty<GatewayMessage<RabbitMqProduceOptions>>());
    }
}
```

## Complete Example

Producer side (publishes integration events from an event store subscription to RabbitMQ):

```csharp
using RabbitMQ.Client;
using Eventuous.RabbitMq.Producers;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Projections.MongoDB;

// ConnectionFactory
var connectionFactory = new ConnectionFactory {
    Uri                    = new(configuration["RabbitMq:ConnectionString"]!),
    DispatchConsumersAsync = true
};
services.AddSingleton(connectionFactory);

// Event store
services.AddEventuousPostgres(configuration.GetSection("Postgres"));
services.AddEventStore<PostgresStore>();

// Checkpoint store (needed for the gateway's source subscription)
services.AddCheckpointStore<MongoCheckpointStore>();

// Producer + Gateway
services.AddProducer<RabbitMqProducer>();
services.AddGateway<
    PostgresAllStreamSubscription,
    PostgresAllStreamSubscriptionOptions,
    RabbitMqProducer,
    RabbitMqProduceOptions>(
    "IntegrationSubscription",
    PaymentsGateway.Transform
);
```

Consumer side (receives integration events from RabbitMQ):

```csharp
using RabbitMQ.Client;
using Eventuous.RabbitMq.Subscriptions;

// ConnectionFactory
var connectionFactory = new ConnectionFactory {
    Uri                    = new(configuration["RabbitMq:ConnectionString"]!),
    DispatchConsumersAsync = true
};
services.AddSingleton(connectionFactory);

// Subscribe to RabbitMQ exchange
services.AddSubscription<RabbitMqSubscription, RabbitMqSubscriptionOptions>(
    "PaymentIntegration",
    builder => builder
        .Configure(x => x.Exchange = "PaymentsIntegration")
        .AddEventHandler<PaymentsIntegrationHandler>()
);
```

## Key Behaviors

- The producer uses publisher confirms (`ConfirmSelect`) and waits for broker acknowledgment.
- The stream name used in `Produce` becomes the RabbitMQ exchange name; exchanges are auto-declared.
- The subscription declares its exchange, queue, and binding automatically on startup.
- Default failure handling rejects and requeues the message (`BasicReject` with `requeue: true`).
- If `FailureHandler` is set and `ThrowOnError` is `false`, a warning is logged about incompatibility.
- Concurrency is managed via `AsyncHandlingFilter`; prefetch defaults to `ConcurrencyLimit * 2`.

## Source Files

- Producer: `src/RabbitMq/src/Eventuous.RabbitMq/Producers/RabbitMqProducer.cs`
- Produce options: `src/RabbitMq/src/Eventuous.RabbitMq/Producers/RabbitMqProduceOptions.cs`
- Exchange options: `src/RabbitMq/src/Eventuous.RabbitMq/Shared/RabbitMqExchangeOptions.cs`
- Subscription: `src/RabbitMq/src/Eventuous.RabbitMq/Subscriptions/RabbitMqSubscription.cs`
- Subscription options: `src/RabbitMq/src/Eventuous.RabbitMq/Subscriptions/RabbitMqSubscriptionOptions.cs`
- Sample (producer): `samples/postgres/Bookings.Payments/Registrations.cs`
- Sample (consumer): `samples/postgres/Bookings/Registrations.cs`
