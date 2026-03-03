---
title: "SQLite"
description: "Supported SQLite infrastructure"
sidebar_position: 5
---

SQLite is a self-contained, serverless, zero-configuration SQL database engine. It is the most widely deployed database engine in the world. [source](https://www.sqlite.org/).

Eventuous supports SQLite as an event store for embedded and local applications (desktop, mobile, CLI tools) that need event sourcing without external database dependencies. It supports catch-up subscriptions to the global event log and to individual streams, as well as projections.

The SQLite implementation uses the `Microsoft.Data.Sqlite` provider and enables WAL (Write-Ahead Logging) mode by default for concurrent read access.

## Data model

Eventuous uses a single table to store events. The table name is `{schema}_messages`, where `{schema}` defaults to `eventuous`. In addition, another table called `{schema}_streams` is used to control the stream existence, and store the last event number for each stream. Events and metadata are stored as TEXT (JSON) columns. The table schema is as follows:

```sql
global_position INTEGER PRIMARY KEY AUTOINCREMENT,
message_id      TEXT    NOT NULL,
message_type    TEXT    NOT NULL,
stream_id       INTEGER NOT NULL REFERENCES streams(stream_id),
stream_position INTEGER NOT NULL,
json_data       TEXT    NOT NULL,
json_metadata   TEXT,
created         TEXT    NOT NULL
```

Since SQLite doesn't support SQL schema namespaces, Eventuous uses a table name prefix instead (e.g. `eventuous_streams`, `eventuous_messages`).

For subscriptions, Eventuous adds a table called `{schema}_checkpoints` that stores the last processed event number for each subscription.

## Event persistence

To register the SQLite event store, use the `AddEventuousSqlite` extension method. This registers the store, schema, and optionally initializes the database on startup:

```csharp title="Program.cs"
builder.Services.AddEventuousSqlite(
    "Data Source=myapp.db",
    schema: "eventuous",
    initializeDatabase: true
);
builder.Services.AddEventStore<SqliteStore>();
```

You can also configure the store using `IConfiguration`:

```json title="appsettings.json"
{
  "SqliteStore": {
    "ConnectionString": "Data Source=myapp.db",
    "Schema": "eventuous",
    "InitializeDatabase": true
  }
}
```

```csharp title="Program.cs"
builder.Services.AddEventuousSqlite(
    builder.Configuration.GetSection("SqliteStore")
);
builder.Services.AddEventStore<SqliteStore>();
```

When that's done, Eventuous will persist aggregates in SQLite when you use the [command service](../../application/app-service).

## Subscriptions

Eventuous supports two types of subscriptions to SQLite: global and stream. The global subscription is a catch-up subscription that reads all events from the global event log. The stream subscription reads events from a specific stream only.

Both subscription types use continuous polling to check for new events.

### Registering subscriptions

Registering a global log subscription:

```csharp title="Program.cs"
builder.Services.AddSubscription<SqliteAllStreamSubscription, SqliteAllStreamSubscriptionOptions>(
    "BookingsProjections",
    builder => builder
        .AddEventHandler<BookingStateProjection>()
        .AddEventHandler<MyBookingsProjection>()
);
```

When you register a subscription to a single stream, you need to configure the subscription options to specify the stream name:

```csharp title="Program.cs"
builder.Services.AddSubscription<SqliteStreamSubscription, SqliteStreamSubscriptionOptions>(
    "StreamSubscription",
    builder => builder
        .Configure(x => x.Stream = "my-stream")
        .AddEventHandler<StreamSubscriptionHandler>()
);
```

### Checkpoint store

Catch-up subscriptions need a [checkpoint](../../subscriptions/checkpoint). You can register the SQLite checkpoint store, and it will be used for all subscriptions in the application:

```csharp title="Program.cs"
builder.Services.AddSqliteCheckpointStore();
```

The checkpoint store uses the same connection string and schema as the event store when registered via `AddEventuousSqlite`.

## Projections

You can use SQLite both as an event store and as a read model store. Eventuous provides a projector base class that allows you to emit SQL statements for events, and the projector will execute them.

Consider the following table schema for the query model:

```sql
CREATE TABLE IF NOT EXISTS bookings (
    booking_id   TEXT NOT NULL PRIMARY KEY,
    checkin_date TEXT,
    price        REAL
);
```

You can project the `BookingImported` event to this table:

```csharp title="ImportingBookingsProjector.cs"
public class ImportingBookingsProjector : SqliteProjector {
    public ImportingBookingsProjector(SqliteConnectionOptions connectionOptions)
        : base(connectionOptions) {
        const string insert = """
            INSERT OR REPLACE INTO bookings
                (booking_id, checkin_date, price)
            VALUES (@booking_id, @checkin_date, @price)
            """;

        On<BookingEvents.BookingImported>(
            (connection, ctx) =>
                Project(
                    connection,
                    insert,
                    new SqliteParameter("@booking_id", ctx.Stream.GetId()),
                    new SqliteParameter("@checkin_date", ctx.Message.CheckIn.ToString("o")),
                    new SqliteParameter("@price", ctx.Message.Price)
                )
        );
    }
}
```

You can then register the projector as a subscription handler:

```csharp title="Program.cs"
builder.Services.AddSubscription<SqliteAllStreamSubscription, SqliteAllStreamSubscriptionOptions>(
    "ImportedBookingsProjections",
    builder => builder
        .UseCheckpointStore<SqliteCheckpointStore>()
        .AddEventHandler<ImportingBookingsProjector>()
);
```

Using `INSERT OR REPLACE` makes the projection idempotent, so reprocessing events after a failure won't cause errors.
