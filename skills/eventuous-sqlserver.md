# Eventuous SQL Server Integration

NuGet package: `Eventuous.SqlServer`
Namespace: `Eventuous.SqlServer`
Source: `src/SqlServer/src/Eventuous.SqlServer/`

Provides event store, subscriptions, checkpoint store, and projections for SQL Server.

## Setup

### AddEventuousSqlServer

Register the SQL Server event store and schema initializer:

```csharp
// Option 1: Connection string directly
services.AddEventuousSqlServer(
    connectionString: "Server=localhost;Database=mydb;...",
    schema: "eventuous",          // default: "eventuous"
    initializeDatabase: true      // creates schema on startup
);

// Option 2: From IConfiguration
services.AddEventuousSqlServer(configuration.GetSection("SqlServer"));
```

This registers:
- `SqlServerStoreOptions` as singleton
- `SqlServerStore` as singleton (the event store)
- `SchemaInitializer` as hosted service (creates tables/stored procs if `InitializeDatabase = true`)
- `SqlServerConnectionOptions` for shared connection info

### SqlServerStoreOptions

```csharp
public record SqlServerStoreOptions {
    public string? ConnectionString   { get; init; }
    public string  Schema             { get; init; } = "eventuous";
    public bool    InitializeDatabase { get; init; }
}
```

## Event Store

`SqlServerStore` extends `SqlEventStoreBase<SqlConnection, SqlTransaction>` and implements `IEventStore`. Default schema is `"eventuous"`.

```csharp
services.AddEventuousSqlServer(connectionString, initializeDatabase: true);
services.AddEventStore<SqlServerStore>();
```

## Schema

The `Schema` class defines stored procedure names and SQL queries scoped to the configured schema name:
- `append_events`, `read_stream_forwards`, `read_stream_backwards`
- `read_all_forwards`, `check_stream`, `truncate_stream`
- Checkpoint queries for the `Checkpoints` table

`SchemaInitializer` is an `IHostedService` that runs embedded SQL scripts to create the schema. It retries up to 10 times with 5-second delays on `SqlException`.

## Subscriptions

### SqlServerAllStreamSubscription

Subscribes to all events across all streams (uses `read_all_forwards` stored procedure).

```csharp
services.AddSubscription<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions>(
    "MyAllStreamSub",
    builder => builder
        .Configure(o => {
            o.Schema = "eventuous";
            o.ConnectionString = connectionString;  // optional if AddEventuousSqlServer was called
        })
        .AddEventHandler<MyHandler>()
);
```

### SqlServerStreamSubscription

Subscribes to events in a single named stream.

```csharp
services.AddSubscription<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions>(
    "MyStreamSub",
    builder => builder
        .Configure(o => {
            o.Stream = new StreamName("MyStream-123");
            o.ConnectionString = connectionString;
        })
        .AddEventHandler<MyHandler>()
);
```

### Options hierarchy

```
SqlSubscriptionOptionsBase          (Schema, MaxPageSize, PollingInterval)
  -> SqlServerSubscriptionBaseOptions  (+ ConnectionString)
       -> SqlServerAllStreamSubscriptionOptions
       -> SqlServerStreamSubscriptionOptions   (+ Stream)
```

Connection string and schema can come from either the subscription options or `SqlServerConnectionOptions` (registered by `AddEventuousSqlServer`).

## Checkpoint Store

`SqlServerCheckpointStore` implements `ICheckpointStore`. Stores checkpoints in `{schema}.Checkpoints` table.

```csharp
services.AddSqlServerCheckpointStore();
```

This uses the connection string and schema from `SqlServerConnectionOptions` (falls back to `SqlServerCheckpointStoreOptions` if configured separately).

```csharp
public record SqlServerCheckpointStoreOptions {
    public string? Schema           { get; init; }
    public string? ConnectionString { get; init; }
}
```

## Projections

`SqlServerProjector` is the base class for SQL Server read model projections.

```csharp
public class MyProjection : SqlServerProjector {
    public MyProjection(SqlServerConnectionOptions options) : base(options) {
        On<MyEvent>((connection, ctx) =>
            Project(connection,
                "INSERT INTO MyReadModel (Id, Name) VALUES (@id, @name)",
                new SqlParameter("@id", ctx.Message.Id),
                new SqlParameter("@name", ctx.Message.Name)
            )
        );
    }
}
```

Register with a subscription:
```csharp
services.AddSubscription<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions>(
    "MyProjectionSub",
    builder => builder
        .AddEventHandler<MyProjection>()
);
```

## Complete Example

```csharp
services.AddEventuousSqlServer(connectionString, initializeDatabase: true);
services.AddEventStore<SqlServerStore>();
services.AddSqlServerCheckpointStore();

services.AddSubscription<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions>(
    "AllEvents",
    builder => builder
        .AddEventHandler<MyHandler>()
);
```
