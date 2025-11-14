# Eventuous - AI Agent Context

## Repository Overview

**Eventuous** is a production-grade Event Sourcing library for .NET that provides abstractions and implementations for building event-sourced systems following Domain-Driven Design (DDD) tactical patterns.

- **Primary Language:** C#
- **Target Frameworks:** .NET 10, 9, and 8
- **License:** Apache 2.0
- **Copyright:** Eventuous HQ OÜ
- **Repository:** https://github.com/Eventuous/eventuous
- **Documentation:** https://eventuous.dev
- **NuGet Profile:** https://www.nuget.org/profiles/Eventuous

## Project Structure

```
eventuous/
├── src/                          # Source code organized by component
│   ├── Core/                     # Core abstractions and implementations
│   │   ├── src/
│   │   │   ├── Eventuous.Domain/           # Aggregate, State, Events
│   │   │   ├── Eventuous.Persistence/      # Event Store, Aggregate Store
│   │   │   ├── Eventuous.Application/      # Command Services
│   │   │   ├── Eventuous.Subscriptions/    # Event subscriptions
│   │   │   ├── Eventuous.Producers/        # Event producers
│   │   │   ├── Eventuous.Serialization/    # Serialization support
│   │   │   └── Eventuous.Shared/           # Shared utilities
│   │   ├── gen/                 # Source generators
│   │   └── test/                # Core tests
│   ├── EventStore/              # EventStoreDB integration
│   ├── Postgres/                # PostgreSQL integration
│   ├── SqlServer/               # SQL Server integration
│   ├── Mongo/                   # MongoDB projections
│   ├── RabbitMq/                # RabbitMQ integration
│   ├── Kafka/                   # Apache Kafka integration
│   ├── GooglePubSub/            # Google Pub/Sub integration
│   ├── Azure/                   # Azure Service Bus integration
│   ├── Redis/                   # Redis integration (experimental)
│   ├── Diagnostics/             # OpenTelemetry, Logging
│   ├── Extensions/              # ASP.NET Core extensions
│   ├── Gateway/                 # Event-Driven Architecture gateway
│   ├── Testing/                 # Testing utilities
│   ├── Experimental/            # Experimental features
│   └── Benchmarks/              # Performance benchmarks
├── test/                        # Test helpers and SUTs
├── samples/                     # Sample applications
│   ├── esdb/                    # EventStoreDB samples
│   └── postgres/                # PostgreSQL samples
├── docs/                        # Documentation site (Docusaurus)
└── props/                       # Build properties
```

## Core Concepts

### 1. Aggregates

**Location:** `src/Core/src/Eventuous.Domain/Aggregate.cs`

Aggregates are the primary domain model abstraction. They:
- Maintain state through event sourcing
- Enforce business invariants
- Produce domain events when state changes
- Track original and current versions for optimistic concurrency

```csharp
public abstract class Aggregate<T> where T : State<T>, new()
```

Key methods:
- `Apply<TEvent>()` - Apply event to state and add to changes
- `Load()` - Reconstruct aggregate from event history
- `EnsureExists()` / `EnsureDoesntExist()` - Guard clauses

### 2. State

Aggregate state is immutable and reconstructed from events using the `When()` pattern. States implement a functional fold pattern.

### 3. Command Services

Eventuous provides two approaches for handling commands:

#### Aggregate-Based Command Services

**Location:** `src/Core/src/Eventuous.Application/AggregateService/CommandService.cs`

Aggregate-based command services work with aggregate instances and orchestrate the aggregate lifecycle:
1. Extract aggregate ID from command
2. Load aggregate from event store (if existing)
3. Execute domain logic on aggregate
4. Persist new events
5. Return result (success or error)

```csharp
public abstract class CommandService<TAggregate, TState, TId>
```

Command handlers are registered using fluent API:
```csharp
On<BookRoom>()
    .InState(ExpectedState.New)
    .GetId(cmd => new BookingId(cmd.BookingId))
    .ActAsync((booking, cmd, _) => booking.BookRoom(...));
```

#### Functional Command Services

**Location:** `src/Core/src/Eventuous.Application/FunctionalService/CommandService.cs`

Functional command services provide an alternative approach without aggregates, using pure functions:
1. Extract stream name from command
2. Load events from stream (if existing)
3. Restore state from events
4. Execute stateless function that produces new events
5. Persist new events
6. Return result (success or error)

```csharp
public abstract class CommandService<TState> where TState : State<TState>, new()
```

Command handlers use a fluent API with stream-based operations:
```csharp
On<BookRoom>()
    .InState(ExpectedState.New)
    .GetStream(cmd => new StreamName($"Booking-{cmd.BookingId}"))
    .Act(cmd => new[] { new RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut) });

On<RecordPayment>()
    .InState(ExpectedState.Existing)
    .GetStream(cmd => new StreamName($"Booking-{cmd.BookingId}"))
    .Act((state, events, cmd) => ProducePaymentEvents(state, cmd));
```

Key differences:
- **No aggregates**: Works directly with state and events
- **Pure functions**: Handlers are stateless functions that transform state + command → events
- **Stream-centric**: Operates on streams rather than aggregate instances
- **More flexible**: Can work with different state types for the same stream

### 4. Event Store Abstractions

**Location:** `src/Core/src/Eventuous.Persistence/`

- `IEventStore` - Combined read/write interface
- `IEventReader` - Read events from streams
- `IEventWriter` - Append events to streams
- `IAggregateStore` - Legacy aggregate persistence (now deprecated)

Supported implementations:
- EventStoreDB (`Eventuous.EventStore`)
- PostgreSQL (`Eventuous.Postgresql`)
- Microsoft SQL Server (`Eventuous.SqlServer`)

### 5. Subscriptions

**Location:** `src/Core/src/Eventuous.Subscriptions/`

Subscriptions provide real-time event processing through:
- Event handlers (`IEventHandler`)
- Consume filters and pipes
- Checkpoint management
- Partitioning support

Event handlers implement:
```csharp
public interface IEventHandler {
    string DiagnosticName { get; }
    ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context);
}
```

### 6. Producers

**Location:** `src/Core/src/Eventuous.Producers/`

Event producers publish events to external systems:
- EventStoreDB
- RabbitMQ
- Apache Kafka
- Google Pub/Sub
- Azure Service Bus

### 7. Gateway

**Location:** `src/Gateway/src/Eventuous.Gateway/`

Connects subscriptions with producers for event-driven architectures, enabling cross-bounded-context integration.

## Key Packages

| Package                               | Purpose                               |
|---------------------------------------|---------------------------------------|
| `Eventuous`                           | Umbrella package with core components |
| `Eventuous.Domain`                    | Domain model abstractions             |
| `Eventuous.Persistence`               | Event store abstractions              |
| `Eventuous.Application`               | Command services                      |
| `Eventuous.Subscriptions`             | Event subscriptions                   |
| `Eventuous.Producers`                 | Event producers                       |
| `Eventuous.EventStore`                | EventStoreDB support                  |
| `Eventuous.Postgresql`                | PostgreSQL support                    |
| `Eventuous.SqlServer`                 | SQL Server support                    |
| `Eventuous.RabbitMq`                  | RabbitMQ integration                  |
| `Eventuous.Kafka`                     | Kafka integration                     |
| `Eventuous.GooglePubSub`              | Google Pub/Sub integration            |
| `Eventuous.Projections.MongoDB`       | MongoDB projections                   |
| `Eventuous.Diagnostics.OpenTelemetry` | OpenTelemetry support                 |
| `Eventuous.Extensions.AspNetCore`     | ASP.NET Core integration              |

## Development Practices

### Code Style
- Follow `.editorconfig` settings in the repository
- Use C# nullable reference types
- Prefer immutability where possible
- Follow DDD tactical patterns

### Testing
- Unit tests in `src/*/test/` directories
- Integration tests require infrastructure (EventStoreDB, PostgreSQL, etc.)
- Test helpers available in `test/Eventuous.TestHelpers/`
- Uses TUnit testing framework (see `test/Eventuous.TestHelpers.TUnit/`)

### Contributing
From `CONTRIBUTING.md`:
- Open an issue before large contributions
- Keep PRs focused on single issues
- Respect existing code formatting
- Only contribute your own work
- Be respectful in discussions

### Building
- Solution file: `Eventuous.slnx` (new Visual Studio format)
- Uses `Directory.Packages.props` for centralized package management
- Docker Compose available for infrastructure: `docker-compose.yml`

## Architecture Patterns

### Event Sourcing
- Aggregates are reconstructed from event streams
- All state changes produce domain events
- Events are immutable and append-only
- Optimistic concurrency via version checks

### CQRS (Command Query Responsibility Segregation)
- Commands handled by command services
- Queries via read models (projections)
- Subscriptions update read models asynchronously

### Domain-Driven Design
- Aggregates as consistency boundaries
- Value objects for domain concepts
- Domain events for state changes
- Repository pattern via event stores

## Diagnostic Features

### OpenTelemetry Support
**Location:** `src/Diagnostics/src/Eventuous.Diagnostics.OpenTelemetry/`

Built-in tracing and metrics for:
- Command handling
- Event persistence
- Subscription processing
- Producer operations

### Logging
**Location:** `src/Diagnostics/src/Eventuous.Diagnostics.Logging/`

Integrates with ASP.NET Core logging infrastructure.

## Common Workflows

### Creating a New Aggregate
1. Define state class inheriting from `State<T>`
2. Define domain events
3. Implement `When()` methods for event application
4. Create aggregate class inheriting from `Aggregate<TState>`
5. Add domain methods that call `Apply()`

### Implementing an Aggregate-Based Command Service
1. Create service inheriting `CommandService<TAggregate, TState, TId>`
2. Register command handlers in constructor using `On<TCommand>()`
3. Configure expected state and ID extraction
4. Implement business logic in `Act()` or `ActAsync()`

### Implementing a Functional Command Service
1. Create service inheriting `CommandService<TState>`
2. Register command handlers in constructor using `On<TCommand>()`
3. Configure expected state (New/Existing/Any) and stream name extraction
4. Implement pure functions that return `IEnumerable<object>` (events)
5. For new streams: handler receives only the command
6. For existing streams: handler receives state, original events, and command

### Creating Event Handlers
1. Inherit from `EventHandler` or implement `IEventHandler`
2. Register typed handlers using `On<TEvent>()`
3. Add handler to subscription via `AddHandler()`

### Setting Up Subscriptions
1. Choose subscription provider (EventStoreDB, PostgreSQL, RabbitMQ, etc.)
2. Configure subscription with checkpoint storage
3. Add event handlers
4. Register in DI container
5. Start subscription service

## File Naming Conventions

- Solution: `Eventuous.slnx`
- Projects: `Eventuous.[Component].csproj`
- Interfaces: `I[Name].cs`
- Abstract classes: Descriptive names (e.g., `Aggregate.cs`, `State.cs`)
- Tests: `Eventuous.Tests.[Component].csproj`

## Important Notes for AI Agents

1. **Breaking Changes**: This library is under active development and doesn't follow semantic versioning strictly. Minor versions may introduce breaking changes.

2. **Obsolete APIs**: `IAggregateStore` is deprecated. Use `IEventReader.LoadAggregate<>()` and `IEventWriter.StoreAggregate<>()` extensions instead.

3. **Stream Names**: Aggregates map to event streams using `StreamName` and `StreamNameMap`. Default pattern: `{AggregateType}-{AggregateId}`.

4. **Type Mapping**: Events require registration in `TypeMap` for serialization/deserialization.

5. **Dependency Injection**: Extensive DI extensions available in `Eventuous.Extensions.DependencyInjection`.

6. **Async by Default**: All I/O operations are async. Use `.NoContext()` extension for `ConfigureAwait(false)`.

7. **Diagnostics**: Built-in support for EventSource, OpenTelemetry, and ASP.NET Core logging.

8. **Testing**: Test infrastructure available for aggregate testing, command service testing, and subscription testing. TUnit is the default testing framework. Use async TUnit assertions in tests.

## Related Resources

- **Main Documentation**: https://eventuous.dev
- **Blog**: https://blog.eventuous.dev
- **Discord**: [Eventuous Server](https://discord.gg/ZrqM6vnnmf)
- **YouTube**: https://www.youtube.com/@eventuous
- **Sample Project**: available in `samples/` directory
- **Support**: https://ubiquitous.no

## Contact

For development and production support, contact [Ubiquitous](https://ubiquitous.no).

For sponsorship: https://github.com/sponsors/Eventuous
