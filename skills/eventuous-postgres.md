# Eventuous PostgreSQL Integration

Infrastructure-specific guidance for using Eventuous with PostgreSQL as the event store, subscription source, checkpoint store, and projection target.

## NuGet Packages

```xml
<PackageReference Include="Eventuous.Postgresql" />
<PackageReference Include="Eventuous.Extensions.DependencyInjection" />
```

The `Eventuous.Postgresql` package provides: `PostgresStore`, `PostgresAllStreamSubscription`, `PostgresStreamSubscription`, `PostgresCheckpointStore`, and `PostgresProjector`.

## Namespaces

```csharp
using Eventuous.Postgresql;              // PostgresStore, PostgresStoreOptions, Schema, SchemaInitializer
using Eventuous.Postgresql.Subscriptions; // Subscriptions, checkpoint store, options
using Eventuous.Postgresql.Projections;   // PostgresProjector
```

## Database Setup

Register PostgreSQL infrastructure with `AddEventuousPostgres`. This configures `NpgsqlDataSource`, `NpgsqlConnection`, `PostgresStoreOptions`, `PostgresStore` (singleton), and a `SchemaInitializer` hosted service.

**Option 1: Connection string directly**

```csharp
// Do not hardcode credentials; use secret storage or environment variables
services.AddEventuousPostgres(
    connectionString: configuration["Postgres:ConnectionString"]!,
    schema: "eventuous",           // default: "eventuous"
    initializeDatabase: true,      // default: false; creates schema tables on startup
    configureBuilder: null,        // Action<IServiceProvider, NpgsqlDataSourceBuilder>?
    connectionLifetime: ServiceLifetime.Transient,  // default
    dataSourceLifetime: ServiceLifetime.Singleton   // default
);
```

**Option 2: From IConfiguration section (recommended)**

```csharp
services.AddEventuousPostgres(configuration.GetSection("Postgres"));
```

The configuration section binds to `PostgresStoreOptions`. Store the connection string in user secrets, environment variables, or a vault — not in `appsettings.json`:

```json
{
  "Postgres": {
    "ConnectionString": "Host=localhost;Username=postgres;Password=...;Database=eventuous;",
    "Schema": "eventuous",
    "InitializeDatabase": true
  }
}
```

## Event Store Registration

After `AddEventuousPostgres`, register `PostgresStore` as the `IEventStore`, `IEventReader`, and `IEventWriter`:

```csharp
services.AddEventuousPostgres(connectionString, initializeDatabase: true);
services.AddEventStore<PostgresStore>();
```

`AddEventStore<PostgresStore>()` registers the store as `IEventStore`, `IEventReader`, and `IEventWriter` (with tracing wrappers when diagnostics are enabled).

## Schema Initialization

When `initializeDatabase: true` (or `InitializeDatabase` in config), the `SchemaInitializer` hosted service runs embedded SQL scripts on startup to create the schema, tables, and functions. The default schema name is `"eventuous"`. All database objects (tables, functions, types) are created under that schema.

`PostgresStoreOptions` properties:
- `Schema` (string) -- database schema name, default `"eventuous"`
- `ConnectionString` (string) -- PostgreSQL connection string
- `InitializeDatabase` (bool) -- create schema on startup, default `false`

## Subscriptions

### PostgresAllStreamSubscription

Polls all events across all streams (the global ordered log). Use this for cross-aggregate projections and integrations.

```csharp
services.AddSubscription<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(
    "MySubscription",
    builder => builder
        .AddEventHandler<MyEventHandler>()
        .WithPartitioningByStream(2) // optional: parallel processing partitioned by stream
);
```

`PostgresAllStreamSubscriptionOptions` inherits from `PostgresSubscriptionBaseOptions` (no additional properties).

### PostgresStreamSubscription

Polls events from a single named stream. Use this for stream-specific projections.

```csharp
services.AddSubscription<PostgresStreamSubscription, PostgresStreamSubscriptionOptions>(
    "MyStreamSub",
    builder => builder
        .Configure(options => options.Stream = new StreamName("MyStream-123"))
        .AddEventHandler<MyStreamHandler>()
);
```

`PostgresStreamSubscriptionOptions` adds:
- `Stream` (StreamName) -- the stream name to subscribe to

### Common Subscription Options (PostgresSubscriptionBaseOptions)

Inherited from `SqlSubscriptionOptionsBase`:

| Property | Type | Default | Description |
|---|---|---|---|
| `Schema` | string | `"eventuous"` | Database schema name |
| `ConcurrencyLimit` | int | 1 | Number of concurrent message consumers |
| `MaxPageSize` | int | 1024 | Messages fetched per poll |
| `Polling` | PollingOptions | see below | Polling interval configuration |
| `Retry` | RetryOptions | see below | Retry configuration |
| `GapAgeThresholdMs` | int? | 3600000 (1h) | Gaps older than this are skipped |
| `GapSkipTimeoutMs` | int? | 5000 | Max time a gap holds back the subscription |
| `GapHandlingTimeoutMs` | int? | null | When set, creates tombstones for persistent gaps |

**PollingOptions**: `MinIntervalMs` (5), `MaxIntervalMs` (1000), `GrowFactor` (1.5)
**RetryOptions**: `InitialDelayMs` (50)

Configure options inline:

```csharp
services.AddSubscription<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(
    "FastSub",
    builder => builder
        .Configure(o => {
            o.ConcurrencyLimit = 4;
            o.MaxPageSize = 512;
            o.Polling = new() { MinIntervalMs = 10, MaxIntervalMs = 500 };
        })
        .AddEventHandler<MyHandler>()
);
```

## Checkpoint Store

`PostgresCheckpointStore` stores subscription checkpoints in the `{schema}.checkpoints` table (created by schema initialization).

Register it using the extension method:

```csharp
services.AddPostgresCheckpointStore();
```

This resolves `NpgsqlDataSource` and `PostgresStoreOptions` from DI. It uses the same schema as the event store by default. To override the schema, configure `PostgresCheckpointStoreOptions`:

```csharp
services.Configure<PostgresCheckpointStoreOptions>(o => o.Schema = "my_schema");
services.AddPostgresCheckpointStore();
```

## Projections with PostgresProjector

`PostgresProjector` is an abstract base class for projecting events into PostgreSQL read model tables. It extends `EventHandler` and provides helper methods for building `NpgsqlCommand` instances.

```csharp
public class BookingProjection : PostgresProjector {
    public BookingProjection(NpgsqlDataSource dataSource) : base(dataSource) {
        // Synchronous handler: returns NpgsqlCommand
        On<BookingEvents.BookingCreated>((connection, ctx) =>
            Project(connection,
                "insert into bookings (id, guest) values (@id, @guest) on conflict (id) do update set guest = @guest",
                ("id", ctx.Stream.GetId()),
                ("guest", ctx.Message.GuestId)
            )
        );

        // Async handler: returns ValueTask<NpgsqlCommand>
        On<BookingEvents.BookingConfirmed>(async (connection, ctx) => {
            // Can do async work before returning the command
            return Project(connection,
                "update bookings set confirmed = true where id = @id",
                ("id", ctx.Stream.GetId())
            );
        });
    }
}
```

**Key API:**
- `On<T>(ProjectToPostgres<T> handler)` -- register sync handler (returns `NpgsqlCommand`)
- `On<T>(ProjectToPostgresAsync<T> handler)` -- register async handler (returns `ValueTask<NpgsqlCommand>`)
- `Project(NpgsqlConnection, string commandText, params (string Name, object Value)[] parameters)` -- static helper that creates a parameterized `NpgsqlCommand`
- `Project(NpgsqlConnection, string commandText, params NpgsqlParameter[] parameters)` -- overload accepting `NpgsqlParameter` array

Register the projector as an event handler on a subscription:

```csharp
services.AddSubscription<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(
    "ReadModelProjection",
    builder => builder.AddEventHandler<BookingProjection>()
);
```

## Complete Registration Example

```csharp
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;

public static class EventuousRegistrations {
    public static void AddEventuous(this IServiceCollection services, IConfiguration configuration) {
        // 1. PostgreSQL infrastructure (data source, store options, schema initializer)
        services.AddEventuousPostgres(configuration.GetSection("Postgres"));

        // 2. Event store (IEventStore, IEventReader, IEventWriter)
        services.AddEventStore<PostgresStore>();

        // 3. Command service
        services.AddCommandService<BookingsCommandService, BookingState>();

        // 4. Checkpoint store in PostgreSQL
        services.AddPostgresCheckpointStore();

        // 5. All-stream subscription with projections
        services.AddSubscription<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(
            "BookingsProjections",
            builder => builder
                .AddEventHandler<BookingProjection>()
                .WithPartitioningByStream(2)
        );

        // 6. Single-stream subscription
        services.AddSubscription<PostgresStreamSubscription, PostgresStreamSubscriptionOptions>(
            "PaymentStream",
            builder => builder
                .Configure(o => o.Stream = new StreamName("Payment-integration"))
                .AddEventHandler<PaymentEventHandler>()
        );
    }
}
```

## Source Code Locations

- Store and options: `src/Postgres/src/Eventuous.Postgresql/PostgresStore.cs`
- Registration extensions: `src/Postgres/src/Eventuous.Postgresql/Extensions/RegistrationExtensions.cs`
- Schema and initialization: `src/Postgres/src/Eventuous.Postgresql/Schema.cs`, `SchemaInitializer.cs`
- Subscriptions: `src/Postgres/src/Eventuous.Postgresql/Subscriptions/`
- Projections: `src/Postgres/src/Eventuous.Postgresql/Projections/PostgresProjector.cs`
- SQL scripts: `src/Postgres/src/Eventuous.Postgresql/Scripts/`
- Sample app: `samples/postgres/Bookings/Registrations.cs`
