# Eventuous Gateway

NuGet package: `Eventuous.Gateway`
Namespace: `Eventuous.Gateway`
Source: `src/Gateway/src/Eventuous.Gateway/`

The Gateway bridges a subscription to a producer, enabling cross-context event routing. It subscribes to events from one source and produces transformed messages to another target.

## Core Concepts

### RouteAndTransform delegate

The central abstraction is a function that transforms incoming events into outgoing messages:

```csharp
public delegate ValueTask<GatewayMessage<TProduceOptions>[]> RouteAndTransform<TProduceOptions>(
    IMessageConsumeContext message
);
```

Return an empty array to skip/filter a message. Return one or more `GatewayMessage` instances to produce them.

### GatewayMessage

```csharp
public record GatewayMessage<TProduceOptions>(
    StreamName       TargetStream,
    object           Message,
    Metadata?        Metadata,
    TProduceOptions  ProduceOptions
);
```

### IGatewayTransform interface

For class-based transforms (registered in DI):

```csharp
public interface IGatewayTransform<TProduceOptions> {
    ValueTask<GatewayMessage<TProduceOptions>[]> RouteAndTransform(IMessageConsumeContext context);
}
```

## Registration

### AddGateway with inline transform

```csharp
services.AddGateway<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>(
    subscriptionId: "MyGateway",
    routeAndTransform: MyTransform.Transform,
    configureSubscription: options => { ... },   // optional
    configureBuilder: builder => { ... },        // optional
    awaitProduce: true                           // default: true
);
```

### AddGateway with DI-resolved RouteAndTransform delegate

```csharp
// Register the delegate in DI
services.AddSingleton<RouteAndTransform<MyProduceOptions>>(MyTransform.Transform);

// Register gateway without passing the delegate
services.AddGateway<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>(
    subscriptionId: "MyGateway"
);
```

### AddGateway with IGatewayTransform class

```csharp
services.AddGateway<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions, MyTransformClass>(
    subscriptionId: "MyGateway"
);
```

The `TTransform` class is registered as a singleton and must implement `IGatewayTransform<TProduceOptions>`.

## Transform Function Examples

### Simple event forwarding with transformation

```csharp
public static class PaymentsGateway {
    static readonly StreamName Stream = new("PaymentsIntegration");

    public static ValueTask<GatewayMessage<KurrentDBProduceOptions>[]> Transform(
        IMessageConsumeContext original
    ) {
        var result = original.Message is PaymentEvents.PaymentRecorded evt
            ? new GatewayMessage<KurrentDBProduceOptions>(
                Stream,
                new BookingPaymentRecorded(
                    original.Stream.GetId(),
                    evt.BookingId,
                    evt.Amount,
                    evt.Currency
                ),
                new Metadata(),
                new KurrentDBProduceOptions()
            )
            : null;

        return ValueTask.FromResult<GatewayMessage<KurrentDBProduceOptions>[]>(
            result != null ? [result] : []
        );
    }
}
```

### Filtering: return empty array to skip

```csharp
public static ValueTask<GatewayMessage<MyOptions>[]> Transform(IMessageConsumeContext ctx) {
    // Only forward specific event types
    if (ctx.Message is not ImportantEvent evt)
        return ValueTask.FromResult(Array.Empty<GatewayMessage<MyOptions>>());

    return ValueTask.FromResult<GatewayMessage<MyOptions>[]>([
        new(new StreamName("target"), evt, null, new MyOptions())
    ]);
}
```

### Producing to RabbitMQ via gateway

```csharp
public static class PaymentsGateway {
    static readonly StreamName             Stream         = new("PaymentsIntegration");
    static readonly RabbitMqProduceOptions ProduceOptions = new();

    public static ValueTask<GatewayMessage<RabbitMqProduceOptions>[]> Transform(
        IMessageConsumeContext original
    ) {
        var result = original.Message is PaymentEvents.PaymentRecorded evt
            ? new GatewayMessage<RabbitMqProduceOptions>(
                Stream,
                new BookingPaymentRecorded(
                    original.Stream.GetId(), evt.BookingId, evt.Amount, evt.Currency
                ),
                new Metadata(),
                ProduceOptions
            )
            : null;

        return ValueTask.FromResult<GatewayMessage<RabbitMqProduceOptions>[]>(
            result != null ? [result] : []
        );
    }
}
```

## Complete Example (EventStoreDB to EventStoreDB)

```csharp
// In the Payments bounded context
services.AddKurrentDBClient(connectionString);
services.AddEventStore<KurrentDBEventStore>();
services.AddCheckpointStore<MongoCheckpointStore>();
services.AddProducer<KurrentDBProducer>();

services.AddGateway<
    AllStreamSubscription,
    AllStreamSubscriptionOptions,
    KurrentDBProducer,
    KurrentDBProduceOptions
>(
    "IntegrationSubscription",
    PaymentsGateway.Transform
);
```

## Complete Example (Postgres to RabbitMQ)

```csharp
services.AddEventuousPostgres(configuration.GetSection("Postgres"));
services.AddEventStore<PostgresStore>();
services.AddCheckpointStore<MongoCheckpointStore>();
services.AddProducer<RabbitMqProducer>();

services.AddGateway<
    PostgresAllStreamSubscription,
    PostgresAllStreamSubscriptionOptions,
    RabbitMqProducer,
    RabbitMqProduceOptions
>(
    "IntegrationSubscription",
    PaymentsGateway.Transform
);
```

## Metadata Propagation

The gateway automatically:
- Sets causation ID from the original message's ID on produced messages
- Passes original context metadata (message, stream, positions) as additional headers on produced messages

Access original context in a custom producer via `ProducedMessageExtensions`:
```csharp
message.GetOriginalStream()
message.GetOriginalMessage()
message.GetOriginalMetadata()
message.GetOriginalStreamPosition()
message.GetOriginalGlobalPosition()
message.GetOriginalMessageId()
message.GetOriginalMessageType()
```

## awaitProduce Option

- `true` (default): The handler awaits produce completion and returns `EventHandlingStatus.Success`. Checkpoint advances only after successful produce.
- `false`: Returns `EventHandlingStatus.Pending` immediately. Uses async acknowledgement via `AsyncConsumeContext`. Useful for high-throughput scenarios but requires the subscription to support async acks.
