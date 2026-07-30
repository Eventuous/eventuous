# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What Is Eventuous

Eventuous is a production-grade Event Sourcing library for .NET implementing DDD tactical patterns. It provides abstractions and implementations for aggregates, command services, event stores, subscriptions, producers, and projections.

## Build & Test Commands

```bash
# Build the entire solution
dotnet build Eventuous.slnx

# Run all tests (all target frameworks: net8.0, net9.0, net10.0)
dotnet test Eventuous.slnx

# Run tests for a specific framework
dotnet test Eventuous.slnx -f net10.0

# Run a single test project
dotnet test src/Core/test/Eventuous.Tests/Eventuous.Tests.csproj

# Run a single test by name filter
dotnet test src/Core/test/Eventuous.Tests/Eventuous.Tests.csproj --filter "FullyQualifiedName~TestClassName"

# CI configuration (used in pull requests)
dotnet test -c "Debug CI" -f net10.0
```

The solution file is `Eventuous.slnx` (new .slnx format, not .sln). The test runner is Microsoft.Testing.Platform with TUnit as the test framework (not xUnit or NUnit). Test results output as TRX to `test-results/`.

Integration tests require infrastructure services. Start them with:
```bash
docker compose up -d
```
Services: EventStoreDB (:2113), PostgreSQL (:5432), MongoDB (:27017), RabbitMQ (:5672), Kafka (:9092), SQL Server (:1433).

## Architecture

### Core Domain Model

**Aggregates** (`src/Core/src/Eventuous.Domain/`): `Aggregate<T> where T : State<T>, new()` — tracks pending `Changes` and `Original` events, enforces business invariants via `Apply<TEvent>()`, uses optimistic concurrency through version tracking.

**State** (`src/Core/src/Eventuous.Domain/`): `State<T>` is an abstract record reconstructed from events using the `When(object @event)` fold pattern. States are immutable.

**Id** (`src/Core/src/Eventuous.Domain/`): `Id` is an abstract record for string-based identity values with validation.

### Command Services (Two Approaches)

**Aggregate-based** (`src/Core/src/Eventuous.Application/AggregateService/`): `CommandService<TAggregate, TState, TId>` — loads aggregate, executes domain logic, persists events. Handlers registered via `On<TCommand>().InState(...).GetId(...).Act(...)`.

**Functional** (`src/Core/src/Eventuous.Application/FunctionalService/`): `CommandService<TState>` — no aggregate instances, pure functions that take state + command and return events. Uses `On<TCommand>().InState(...).GetStream(...).Act(...)`.

### Event Store Layer

`IEventStore` (combined), `IEventReader`, `IEventWriter` in `src/Core/src/Eventuous.Persistence/`. Implementations: EventStoreDB (`src/EventStore/`), PostgreSQL (`src/Postgres/`), SQL Server (`src/SqlServer/`).

`IAggregateStore` is **deprecated** — use `IEventReader.LoadAggregate<>()` and `IEventWriter.StoreAggregate<>()` extension methods instead.

### Subscriptions & Producers

**Subscriptions** (`src/Core/src/Eventuous.Subscriptions/`): `IEventHandler` processes events, with consume filters/pipes, checkpoint management, and partitioning support.

**Producers** (`src/Core/src/Eventuous.Producers/`): `IProducer`/`BaseProducer` for publishing to RabbitMQ, Kafka, Google Pub/Sub, Azure Service Bus.

**Gateway** (`src/Gateway/`): Connects subscriptions to producers for cross-context event routing.

### Key Conventions

- **Stream naming**: Default pattern is `{AggregateType}-{AggregateId}` via `StreamNameMap`.
- **Type mapping**: Events must be registered in `TypeMap` for serialization.
- **Async everywhere**: All I/O is async; use `.NoContext()` for `ConfigureAwait(false)`.
- **Diagnostics**: Built-in OpenTelemetry tracing/metrics in `src/Diagnostics/`.

## Project Layout

```
src/Core/src/         Core packages (Domain, Persistence, Application, Subscriptions, Producers, Serialization, Shared)
src/Core/gen/         Source generators
src/Core/test/        Core test projects
src/EventStore/       EventStoreDB integration (src/ + test/)
src/Postgres/         PostgreSQL integration (src/ + test/)
src/SqlServer/        SQL Server integration (src/ + test/)
src/Mongo/            MongoDB projections
src/RabbitMq/         RabbitMQ integration
src/Kafka/            Kafka integration
src/GooglePubSub/     Google Pub/Sub integration
src/Azure/            Azure Service Bus integration
src/Extensions/       ASP.NET Core, DI extensions
src/Diagnostics/      OpenTelemetry, Logging
src/Gateway/          Event gateway
src/Testing/          Test utilities
test/                 Shared test helpers (Eventuous.Sut.App, Eventuous.Sut.Domain, Eventuous.TestHelpers, Eventuous.TestHelpers.TUnit)
samples/              Sample apps (esdb, postgres, kurrentdb, banking)
```

## External Repos to Update

When making public API changes or adding new components, the following separate repos may need updates:

- **Documentation site**: https://github.com/Eventuous/eventuous-docs — public API additions, removals, or changes must be reflected in the docs.
- **Claude Code plugin**: https://github.com/Eventuous/eventuous-plugin — skills and agent instructions may need updating to reflect new patterns, components, or conventions.

## Code Style

- Targets .NET 10/9/8 (`TargetFrameworks: net10.0;net9.0;net8.0`)
- C# preview language features (`LangVersion: preview`)
- Nullable reference types enabled
- Implicit usings enabled
- Follow `.editorconfig` formatting rules
- Root namespace is `Eventuous` for most projects; integration-specific projects use `Eventuous.PostgreSQL`, `Eventuous.EventStore`, etc.
- Centralized package versions in `Directory.Packages.props`
- Versioning via MinVer from Git tags
