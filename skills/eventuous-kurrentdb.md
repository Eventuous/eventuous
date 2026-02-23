# Eventuous KurrentDB (EventStoreDB) Integration

Infrastructure-specific guidance for using Eventuous with KurrentDB (EventStoreDB) as the event store, subscription source, and producer target.

## NuGet Packages

```xml
<PackageReference Include="Eventuous.KurrentDB" />
<PackageReference Include="Eventuous.Extensions.DependencyInjection" />
```

The `Eventuous.KurrentDB` package provides: `KurrentDBEventStore`, `AllStreamSubscription`, `StreamSubscription`, `StreamPersistentSubscription`, `AllPersistentSubscription`, and `KurrentDBProducer`. It depends on `KurrentDB.Client`.

## Namespaces

```csharp
using Eventuous.KurrentDB;                // KurrentDBEventStore
using Eventuous.KurrentDB.Subscriptions;  // AllStreamSubscription, StreamSubscription, StreamPersistentSubscription, AllPersistentSubscription, options
using Eventuous.KurrentDB.Producers;      // KurrentDBProducer, KurrentDBProduceOptions
```

## Client Registration

Register the KurrentDB gRPC client using `AddKurrentDBClient` (provided by the `KurrentDB.Client` package):

```csharp
services.AddKurrentDBClient("esdb://localhost:2113?tls=false");
```

Or from configuration:

```csharp
services.AddKurrentDBClient(configuration["EventStore:ConnectionString"]!);
```

This registers `KurrentDBClient` as a singleton in the DI container. All Eventuous KurrentDB types (`KurrentDBEventStore`, subscriptions, producer) resolve this client automatically.

## Event Store Registration

Register `KurrentDBEventStore` as `IEventStore`, `IEventReader`, and `IEventWriter`:

```csharp
services.AddKurrentDBClient("esdb://localhost:2113?tls=false");
services.AddEventStore<KurrentDBEventStore>();
```

`KurrentDBEventStore` implements `IEventStore` (which combines `IEventReader` and `IEventWriter`). `AddEventStore<T>()` registers all three interfaces, with tracing wrappers when diagnostics are enabled.

The legacy class `EsdbEventStore` is obsolete -- use `KurrentDBEventStore` instead.

## Subscriptions

### AllStreamSubscription (Catch-Up, $all Stream)

Subscribes to the `$all` global stream. Requires a checkpoint store. Use this for cross-aggregate projections and integrations.

```csharp
services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
    "BookingsProjections",
    builder => builder
        .UseCheckpointStore<MongoCheckpointStore>()
        .AddEventHandler<BookingStateProjection>()
        .AddEventHandler<MyBookingsProjection>()
        .WithPartitioningByStream(2)
);
```

`AllStreamSubscriptionOptions` properties:

| Property | Type | Default | Description |
|---|---|---|---|
| `EventFilter` | IEventFilter? | null | Server-side event filter (defaults to excluding system events) |
| `CheckpointInterval` | uint | 10 | How often to persist checkpoint when filtering skips events |
| `ConcurrencyLimit` | int | 1 | Number of concurrent message consumers |
| `ResolveLinkTos` | bool | false | Resolve link events to their targets |
| `Credentials` | UserCredentials? | null | Optional user credentials |

Inherited from `SubscriptionWithCheckpointOptions`:

| Property | Type | Default | Description |
|---|---|---|---|
| `CheckpointCommitBatchSize` | int | 100 | Commit checkpoint after this many events |
| `CheckpointCommitDelayMs` | int | 5000 | Commit checkpoint after this delay (ms) |
| `StartFrom` | InitialPosition | Earliest | Where to start if no checkpoint exists (`Earliest` or `Latest`) |

### StreamSubscription (Catch-Up, Specific Stream)

Subscribes to a single named stream. Requires a checkpoint store.

```csharp
services.AddSubscription<StreamSubscription, StreamSubscriptionOptions>(
    "MyStreamSub",
    builder => builder
        .Configure(o => o.StreamName = "MyStream-123")
        .UseCheckpointStore<MongoCheckpointStore>()
        .AddEventHandler<MyStreamHandler>()
);
```

`StreamSubscriptionOptions` adds:

| Property | Type | Default | Description |
|---|---|---|---|
| `StreamName` | StreamName | (required) | The stream to subscribe to |
| `IgnoreSystemEvents` | bool | true | Skip events whose type starts with `$` |

Plus all properties from `AllStreamSubscriptionOptions` above (except `EventFilter` and `CheckpointInterval`).

### StreamPersistentSubscription (Persistent, Specific Stream)

Server-managed persistent subscription for a specific stream. KurrentDB manages the checkpoint -- no checkpoint store needed. The subscription is auto-created if it does not exist.

```csharp
services.AddSubscription<StreamPersistentSubscription, StreamPersistentSubscriptionOptions>(
    "PaymentIntegration",
    builder => builder
        .Configure(x => x.StreamName = "PaymentEvents")
        .AddEventHandler<PaymentsIntegrationHandler>()
);
```

`StreamPersistentSubscriptionOptions` properties:

| Property | Type | Default | Description |
|---|---|---|---|
| `StreamName` | StreamName | (required) | The stream to subscribe to |
| `SubscriptionSettings` | PersistentSubscriptionSettings? | null | Native KurrentDB persistent subscription settings |
| `BufferSize` | int | 10 | Subscription buffer size |
| `Deadline` | TimeSpan? | null | gRPC call deadline |
| `FailureHandler` | HandleEventProcessingFailure? | null | Custom failure handling (default: retry then NACK) |
| `ResolveLinkTos` | bool | false | Resolve link events |
| `Credentials` | UserCredentials? | null | Optional user credentials |

### AllPersistentSubscription (Persistent, $all Stream)

Server-managed persistent subscription for the `$all` stream.

```csharp
services.AddSubscription<AllPersistentSubscription, AllPersistentSubscriptionOptions>(
    "AllPersistent",
    builder => builder
        .Configure(x => x.EventFilter = EventTypeFilter.ExcludeSystemEvents())
        .AddEventHandler<MyHandler>()
);
```

`AllPersistentSubscriptionOptions` adds:

| Property | Type | Default | Description |
|---|---|---|---|
| `EventFilter` | IEventFilter? | null | Server-side event filter (set at subscription creation time, not updated afterward) |

Plus all properties from `PersistentSubscriptionOptions` above.

## Producer

`KurrentDBProducer` appends events to KurrentDB streams. Register it with `AddProducer`:

```csharp
services.AddProducer<KurrentDBProducer>();
```

Use the producer to publish events:

```csharp
await producer.Produce("target-stream", message, cancellationToken: ct);
```

`KurrentDBProduceOptions` controls produce behavior:

| Property | Type | Default | Description |
|---|---|---|---|
| `ExpectedState` | StreamState | Any | Expected stream state for optimistic concurrency |
| `MaxAppendEventsCount` | int | 500 | Max events per batch append |
| `Deadline` | TimeSpan? | null | Timeout for the produce operation |
| `Credentials` | UserCredentials? | null | Optional user credentials |

## Checkpoint Stores

KurrentDB itself does not provide a checkpoint store. For catch-up subscriptions (`AllStreamSubscription`, `StreamSubscription`), you need an external checkpoint store. Common choices:

- **MongoDB**: `MongoCheckpointStore` from `Eventuous.Projections.MongoDB`
- **PostgreSQL**: `PostgresCheckpointStore` from `Eventuous.Postgresql`
- **SQL Server**: `SqlServerCheckpointStore` from `Eventuous.SqlServer`
- **Redis**: `RedisCheckpointStore` from `Eventuous.Redis`

Register globally or per-subscription:

```csharp
// Global default
services.AddCheckpointStore<MongoCheckpointStore>();

// Per-subscription override
services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
    "MySub",
    builder => builder
        .UseCheckpointStore<MongoCheckpointStore>()
        .AddEventHandler<MyHandler>()
);
```

Persistent subscriptions (`StreamPersistentSubscription`, `AllPersistentSubscription`) do not need a checkpoint store -- KurrentDB manages the position server-side.

## Gateway (Subscription-to-Producer Bridge)

Use `AddGateway` to route events from a subscription through a producer to another stream or system:

```csharp
services.AddCheckpointStore<MongoCheckpointStore>();
services.AddProducer<KurrentDBProducer>();

services.AddGateway<AllStreamSubscription, AllStreamSubscriptionOptions, KurrentDBProducer, KurrentDBProduceOptions>(
    "IntegrationSubscription",
    PaymentsGateway.Transform
);
```

## Complete Registration Example

```csharp
using Eventuous.KurrentDB;
using Eventuous.KurrentDB.Subscriptions;
using Eventuous.KurrentDB.Producers;
using Eventuous.Projections.MongoDB;

public static class EventuousRegistrations {
    public static void AddEventuous(this IServiceCollection services, IConfiguration configuration) {
        // 1. KurrentDB client
        services.AddKurrentDBClient(configuration["EventStore:ConnectionString"]!);

        // 2. Event store (IEventStore, IEventReader, IEventWriter)
        services.AddEventStore<KurrentDBEventStore>();

        // 3. Command service
        services.AddCommandService<BookingsCommandService, BookingState>();

        // 4. All-stream catch-up subscription with MongoDB checkpoint store
        services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
            "BookingsProjections",
            builder => builder
                .UseCheckpointStore<MongoCheckpointStore>()
                .AddEventHandler<BookingStateProjection>()
                .AddEventHandler<MyBookingsProjection>()
                .WithPartitioningByStream(2)
        );

        // 5. Persistent subscription (no checkpoint store needed)
        services.AddSubscription<StreamPersistentSubscription, StreamPersistentSubscriptionOptions>(
            "PaymentIntegration",
            builder => builder
                .Configure(x => x.StreamName = PaymentsIntegrationHandler.Stream)
                .AddEventHandler<PaymentsIntegrationHandler>()
        );

        // 6. Producer for publishing to KurrentDB streams
        services.AddProducer<KurrentDBProducer>();
    }
}
```

## Source Code Locations

- Event store: `src/KurrentDB/src/Eventuous.KurrentDB/KurrentDBEventStore.cs`
- Subscriptions: `src/KurrentDB/src/Eventuous.KurrentDB/Subscriptions/`
- Subscription options: `src/KurrentDB/src/Eventuous.KurrentDB/Subscriptions/Options/`
- Producer: `src/KurrentDB/src/Eventuous.KurrentDB/Producers/KurrentDBProducer.cs`
- Produce options: `src/KurrentDB/src/Eventuous.KurrentDB/Producers/KurrentDBProduceOptions.cs`
- Store registration extensions: `src/Extensions/src/Eventuous.Extensions.DependencyInjection/Registrations/Stores.cs`
- Sample app: `samples/kurrentdb/Bookings/Registrations.cs`
