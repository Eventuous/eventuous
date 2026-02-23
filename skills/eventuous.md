# Eventuous - Event Sourcing for .NET

Eventuous is a production-grade Event Sourcing library for .NET that implements DDD tactical patterns. It provides aggregates, command services, event stores, subscriptions, producers, projections, and gateway components.

**Target frameworks:** .NET 10/9/8. C# preview language features, nullable reference types, and implicit usings enabled.

## Infrastructure-Specific Guides

When working with specific infrastructure, also include the relevant skill file for full registration and configuration details:

- `eventuous-kurrentdb.md` - KurrentDB (EventStoreDB) event store, subscriptions, producer
- `eventuous-postgres.md` - PostgreSQL event store, subscriptions, projections
- `eventuous-sqlserver.md` - SQL Server event store, subscriptions
- `eventuous-mongodb.md` - MongoDB projections and checkpoint store
- `eventuous-rabbitmq.md` - RabbitMQ producer and subscription
- `eventuous-kafka.md` - Kafka producer and subscription
- `eventuous-google-pubsub.md` - Google Pub/Sub producer and subscription
- `eventuous-azure-servicebus.md` - Azure Service Bus producer and subscription
- `eventuous-gateway.md` - Event gateway for cross-context routing

---

## Domain Model

### Identity

Strongly-typed aggregate IDs extend the abstract `Id` record. The base class validates non-empty strings and provides implicit string conversion.

```csharp
public record BookingId(string Value) : Id(Value);
```

### Domain Events

Events are immutable records decorated with `[EventType]` for serialization mapping. Group events in a static class with versioned nested classes for schema evolution. Use primitive types in events, not value objects.

```csharp
public static class BookingEvents {
    public static class V1 {
        [EventType("V1.RoomBooked")]
        public record RoomBooked(
            string    GuestId,
            string    RoomId,
            LocalDate CheckInDate,
            LocalDate CheckOutDate,
            float     BookingPrice,
            string    Currency
        );

        [EventType("V1.BookingCancelled")]
        public record BookingCancelled(string Reason);
    }
}
```

### State

State is an abstract record reconstructed from events. Register event handlers in the parameterless constructor using `On<TEvent>()`. Handlers are static pure functions that return new state via `with` expressions.

```csharp
public record BookingState : State<BookingState> {
    public string  GuestId { get; init; } = null!;
    public RoomId  RoomId  { get; init; } = null!;
    public Money   Price   { get; init; } = null!;
    public bool    Paid    { get; init; }

    public BookingState() {
        On<V1.RoomBooked>(HandleBooked);
        On<V1.BookingCancelled>((state, _) => state with { Cancelled = true });
    }

    static BookingState HandleBooked(BookingState state, V1.RoomBooked e)
        => state with {
            GuestId = e.GuestId,
            RoomId  = new(e.RoomId),
            Price   = new(e.BookingPrice, e.Currency)
        };
}
```

For identity-aware state, use `State<T, TId>` which adds an `Id` property set automatically on load:

```csharp
public record BookingState : State<BookingState, BookingId> { ... }
```

### Aggregate

Aggregates contain business logic and invariant enforcement. They extend `Aggregate<TState>` and use `Apply<TEvent>()` to record events and update state.

Key members:
- `State` - current aggregate state after all applied events
- `Changes` - pending events not yet persisted
- `Apply<TEvent>(evt)` - apply event, update state, add to pending changes
- `EnsureExists()` / `EnsureDoesntExist()` - invariant guards
- `OriginalVersion` / `CurrentVersion` - optimistic concurrency tracking

```csharp
public class Booking : Aggregate<BookingState> {
    public void BookRoom(string guestId, RoomId roomId, StayPeriod period, Money price) {
        EnsureDoesntExist();
        Apply(new V1.RoomBooked(guestId, roomId, period.CheckIn, period.CheckOut, price.Amount, price.Currency));
    }

    public void Cancel(string reason) {
        EnsureExists();
        Apply(new V1.BookingCancelled(reason));
    }
}
```

### Value Objects

Value objects are records with validation in constructors and internal parameterless constructors for serialization:

```csharp
public record Money {
    public float  Amount   { get; internal init; }
    public string Currency { get; internal init; } = null!;

    internal Money() { }

    public Money(float amount, string currency) {
        if (amount < 0) throw new DomainException("Amount cannot be negative");
        Amount = amount;
        Currency = currency;
    }
}
```

### Domain Services

Define external service contracts as delegates in the domain layer:

```csharp
public static class Services {
    public delegate ValueTask<bool> IsRoomAvailable(RoomId roomId, StayPeriod period);
}
```

---

## Command Services

### Commands

Commands are record types, optionally grouped in a static class:

```csharp
public static class BookingCommands {
    public record BookRoom(string BookingId, string GuestId, string RoomId, DateTime CheckIn, DateTime CheckOut, float Price, string Currency);
    public record CancelBooking(string BookingId, string Reason);
}
```

### Aggregate-Based Command Service

For rich domain models with aggregate classes. Extends `CommandService<TAggregate, TState, TId>`. Register handlers in the constructor using the fluent builder chain: `On<TCommand>().InState(...).GetId(...).Act(...)`.

```csharp
public class BookingsCommandService : CommandService<Booking, BookingState, BookingId> {
    public BookingsCommandService(IEventStore store, Services.IsRoomAvailable isRoomAvailable)
        : base(store) {
        On<BookRoom>()
            .InState(ExpectedState.New)
            .GetId(cmd => new BookingId(cmd.BookingId))
            .Act((booking, cmd) => booking.BookRoom(
                cmd.GuestId,
                new RoomId(cmd.RoomId),
                new StayPeriod(cmd.CheckIn, cmd.CheckOut),
                new Money(cmd.Price, cmd.Currency)
            ));

        On<CancelBooking>()
            .InState(ExpectedState.Existing)
            .GetId(cmd => new BookingId(cmd.BookingId))
            .Act((booking, cmd) => booking.Cancel(cmd.Reason));
    }
}
```

**Fluent builder chain (aggregate-based):**
1. `On<TCommand>()` - register handler for command type
2. `.InState(ExpectedState)` - `New`, `Existing`, or `Any`
3. `.GetId(cmd => ...)` - extract aggregate ID from command (or `.GetIdAsync(...)`)
4. Optional: `.AmendEvent(...)` - modify events before storage
5. Optional: `.ResolveStore(...)` / `.ResolveReader(...)` / `.ResolveWriter(...)` - per-command store resolution
6. `.Act((aggregate, cmd) => ...)` - sync action, or `.ActAsync((aggregate, cmd, ct) => ...)` for async

### Functional Command Service

For pure-function style without aggregate instances. Extends `CommandService<TState>`. Uses `GetStream` instead of `GetId`, and `Act` returns events instead of calling aggregate methods.

```csharp
public class PaymentsService : CommandService<PaymentState> {
    public PaymentsService(IEventStore store) : base(store) {
        On<RecordPayment>()
            .InState(ExpectedState.New)
            .GetStream(cmd => GetStream(cmd.PaymentId))
            .Act(cmd => [new PaymentRecorded(cmd.BookingId, cmd.Amount, cmd.Currency)]);

        On<RefundPayment>()
            .InState(ExpectedState.Existing)
            .GetStream(cmd => GetStream(cmd.PaymentId))
            .Act((state, events, cmd) => [new PaymentRefunded(cmd.PaymentId, cmd.Reason)]);
    }
}
```

**Fluent builder chain (functional):**
1. `On<TCommand>()` - register handler
2. `.InState(ExpectedState)` - `New`, `Existing`, or `Any`
3. `.GetStream(cmd => ...)` - get stream name (use `GetStream(id)` helper for default naming)
4. `.Act(cmd => events)` - for new streams (returns `NewEvents` / `IEnumerable<object>`)
5. `.Act((state, originalEvents, cmd) => events)` - for existing streams, receives current state

### Result Type

Both command services return `Result<TState>`:

```csharp
var result = await service.Handle(command, cancellationToken);

// Pattern match
result.Match(
    ok => /* ok.State, ok.Changes, ok.GlobalPosition */,
    error => /* error.Exception, error.ErrorMessage */
);

// Or check directly
if (result.Success) {
    var ok = result.Get();
    // ok.State, ok.Changes
}
```

---

## Event Serialization & Type Mapping

Events must be registered in `TypeMap` for serialization. The `[EventType]` attribute provides automatic registration.

```csharp
// Option 1: Auto-discover all [EventType]-decorated types in loaded assemblies
TypeMap.RegisterKnownEventTypes();

// Option 2: Auto-discover from specific assemblies
TypeMap.RegisterKnownEventTypes(typeof(BookingEvents).Assembly);

// Option 3: Manual registration
TypeMap.Instance.AddType<V1.RoomBooked>("V1.RoomBooked");
```

Default serializer uses `System.Text.Json`. Configure custom options:

```csharp
DefaultEventSerializer.SetDefaultSerializer(
    new DefaultEventSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web))
);
```

---

## Stream Naming

Default pattern: `{AggregateType}-{AggregateId}` (e.g., `Booking-booking-123`).

For functional services, `GetStream(id)` uses `{StateNameWithoutSuffix}-{id}`.

Custom mapping:

```csharp
var streamNameMap = new StreamNameMap();
streamNameMap.Register<BookingId>(id => new StreamName($"bookings:{id.Value}"));
// Pass to command service or register in DI
```

Extracting ID from stream name (useful in projections): `ctx.Stream.GetId()`.

---

## HTTP API

### Controller-Based

Extend `CommandHttpApiBase<TState>` and call `Handle()`:

```csharp
[Route("/booking")]
public class BookingCommandApi(ICommandService<BookingState> service)
    : CommandHttpApiBase<BookingState>(service) {

    [HttpPost("book")]
    public Task<ActionResult<Result<BookingState>.Ok>> BookRoom(
        [FromBody] BookRoom cmd, CancellationToken ct) => Handle(cmd, ct);
}
```

### Minimal API with Auto-Discovery

Annotate commands with `[HttpCommand]` and map them:

```csharp
// On command records:
[HttpCommand<BookingState>(Route = "book")]
public record BookRoom(string BookingId, string GuestId, ...);

// Or group commands under a static class:
[HttpCommands<BookingState>]
public static class BookingCommands {
    [HttpCommand(Route = "book")]
    public record BookRoom(...);
}

// In Program.cs:
app.MapDiscoveredCommands<BookingState>();
// Or map individual commands:
app.MapCommand<BookRoom, BookingState>();
```

---

## Subscriptions

Subscriptions deliver events to handlers in real-time. Use them for projections, integration, and event transformation.

### Event Handler

Extend `EventHandler` and register typed handlers:

```csharp
public class PaymentsIntegrationHandler : EventHandler {
    public PaymentsIntegrationHandler(ICommandService<BookingState> service) {
        On<BookingPaymentRecorded>(async ctx => {
            await service.Handle(
                new RecordPayment(ctx.Message.BookingId, ctx.Message.Amount, ctx.Message.Currency),
                ctx.CancellationToken
            );
        });
    }
}
```

The `IMessageConsumeContext<T>` provides: `Message`, `Stream`, `GlobalPosition`, `Metadata`, `CancellationToken`.

### Subscription Registration

```csharp
services.AddSubscription<SubscriptionType, SubscriptionOptionsType>(
    "SubscriptionName",
    builder => builder
        .Configure(opts => { /* configure options */ })
        .UseCheckpointStore<CheckpointStoreType>()
        .AddEventHandler<MyEventHandler>()
        .WithPartitioningByStream(2)  // optional parallel processing
);
```

Subscription types and checkpoint stores are infrastructure-specific (see infrastructure skill files).

---

## Producers

Producers publish messages to brokers or event stores:

```csharp
// Registration
services.AddProducer<ProducerType>();

// Usage (injected)
await producer.Produce(
    new StreamName("target-stream"),
    new[] { new ProducedMessage(eventObject, metadata) },
    cancellationToken
);
```

---

## DI Registration Pattern

Standard setup in `Program.cs` or extension methods:

```csharp
// 1. Configure serialization
DefaultEventSerializer.SetDefaultSerializer(
    new DefaultEventSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web))
);

// 2. Register event store (infrastructure-specific, see infra skill files)
services.AddEventStore<YourEventStoreImpl>();

// 3. Register command services
services.AddCommandService<BookingsCommandService, BookingState>();
// Or for functional:
services.AddCommandService<PaymentsService, PaymentState>();

// 4. Register subscriptions (infrastructure-specific)
services.AddSubscription<SubType, SubOptions>("name", builder => ...);

// 5. Register producers (infrastructure-specific)
services.AddProducer<ProducerType>();
```

---

## Diagnostics

Built-in OpenTelemetry integration:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddEventuousTracing())
    .WithMetrics(b => b
        .AddEventuous()                // app + persistence metrics
        .AddEventuousSubscriptions()   // subscription metrics
    );
```

Disable diagnostics via `EVENTUOUS_DISABLE_DIAGS` environment variable.

Spyglass diagnostic endpoint:

```csharp
app.MapEventuousSpyglass();
```
