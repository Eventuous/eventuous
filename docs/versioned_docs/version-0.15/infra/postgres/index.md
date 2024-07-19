---
title: "PostgreSQL"
description: "Supported PostgreSQL infrastructure"
sidebar_position: 3
---

PostgreSQL is a powerful, open source object-relational database system with over 30 years of active development that has earned it a strong reputation for reliability, feature robustness, and performance. [source](https://www.postgresql.org/).

Eventuous supports Postgres as an event store and also allows subscribing to the global event log and to individual streams using catch-up subscriptions.

## Data model

Eventuous uses a single table to store events. The table name is `messages`. In addition, another table called `streams` is used to control the stream existence, and store the last event number for each stream. In the `messages` table, events and metadata are stored as JSONB columns. The table schema is as follows:

```sql
message_id      uuid,
message_type    varchar not null,
stream_id       integer not null,
stream_position integer not null,
global_position bigint primary key generated always as identity, 
json_data       jsonb not null,
json_metadata   jsonb,
created         timestamp not null,
```

In theory, it allows you to execute queries across events using the JSONB query syntax of Postgres SQL dialect.

For subscriptions, Eventuous adds a table called `checkpoints` that stores the last processed event number for each subscription. It is then used by the checkpoint store implementation for Postgres.

## Event persistence

Before using Postgres as an event store, you need to register the Postgres-based event store implementation.
For that to work, you'd also need to register a Postgres data source, which is used to create connections to the database.
Eventuous provides a few overloads for `AddEventuousPostgres` registration extension to do that.

One way to register the data source is to provide a connection string and, optionally, the schema name:

```csharp titlle="Program.cs"
builder.Services.AddEventuousPostgres(connectionString, "mySchema");
```

If the schema name is not provided, the default schema name (`eventuous`) will be used.

Another way to register the data source is by using configuration options. For example, you can add the following to the settings file:

```json title="appSettings.json"
{
  "PostgresStore": {
    "Schema": "mySchema",
    "ConnectionString": "Host=localhost;Username=postgres;Password=secret;Database=mydb;",
    "InitializeDatabase": true
  }
}
```

Then, use the configuration section to register the data source:

```csharp titlle="Program.cs"
builder.Services.AddEventuousPostgres(
    builder.Configuration.GetSection("PostgresStore")
);
```

The `InitializeDatabase` setting tells Eventuous if it needs to create the schema. If you create the schema in a separate migration application, set this setting to `false`. If the schema cannot be found, and the `InitializeDatabase` setting is set to `false`, the application will fail to start.

Next, you need to register the Postgres event store:

```csharp
builder.Services.AddEventStore<PostgresStore>();
```

When that's done, Eventuous will use Postgres for persistence in command services.

## Subscriptions

Eventuous supports two types of subscriptions to Postgres: global and stream. The global subscription is a catch-up subscription, which means that it reads all events from the beginning of the event log. The stream subscription is also a catch-up subscription, but it only reads events from a specific stream.

Both subscription types use continuous polling to check for new events. We don't use the notification feature of Postgres.

### Registering subscriptions

Registering a global log subscription is similar to [EventStoreDB](../esdb/index.md#all-stream-subscription). The only difference is the subscription and the options types:

```csharp titlle="Program.cs"
builder.Services.AddSubscription<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(
    "BookingsProjections",
    builder => builder
        .AddEventHandler<BookingStateProjection>()
        .AddEventHandler<MyBookingsProjection>();
);
```

When you register a subscription to a single stream, you need to configure the subscription options to specify the stream name:

```csharp titlle="Program.cs"
builder.Services.AddSubscription<PostgresStreamSubscription, PostgresStreamSubscriptionOptions>(
    "StreamSubscription",
    builder => builder
        .Configure(x => x.StreamName = "my-stream")
        .AddEventHandler<StreamSubscriptionHander>()
);
```

As subscriptions use a Postgres data source for opening the connection, there's no need to register additional dependencies apart from calling `AddEventuousPostgres` as described in [event persistence](#event-persistence) section above.

### Checkpoint store

Catch-up subscriptions need a [checkpoint](../../subscriptions/checkpoint). You can register the checkpoint store using `AddCheckpointStore<T>`, and it will be used for all subscriptions in the application.

Remember to store the checkpoint in the same database as the read model. For example, if you use Postgres as an event store, and project events to read models in MongoDB, you need to use the `MongoCheckpointStore`. Eventuous also has a checkpoint store implementation for Postgres (`PostgresCheckpointStore`), which you can use if you project events to Postgres.

When using the Postgres checkpoint store, you can register it using a dedicated extension function:

```csharp
builder.Services.AddPostgresCheckpointStore();
```

This registration function will use the schema name provided when you register the data source using `AddEventuousPostgres`.

### Projections

You can use Postgres both as an event store and as a read model store. In that case, you can use the same connection factory for both the event store, the checkpoint store, and the projector.

Eventuous provides a simple projector base class, which allows you to emit SQL statements for the events you want to project, and the projector will execute them.

Consider the following table schema for the query model:

```sql
create table if not exists myschema.bookings (
    booking_id varchar(1000) not null primary key,
    checkin_date timestamp,
    price numeric(10,2)
);
```

You can project the `BookingImported` event to this table using a simple projector:

```csharp title="ImportingBookingsProjector.cs"
public class ImportingBookingsProjector : PostgresProjector {
    public ImportingBookingsProjector(NpgsqlDataSource dataSource) : base(dataSource) {
        const string insert = @"insert into myschema.bookings 
            (booking_id, checkin_date, price) 
            values (@booking_id, @checkin_date, @price)";

        On<BookingEvents.BookingImported>(
            (connection, ctx) => 
                Project(
                    connection,
                    insert,
                    new NpgsqlParameter("@booking_id", ctx.Stream.GetId()),
                    new NpgsqlParameter("@checkin_date", ctx.Message.CheckIn.ToDateTimeUnspecified()),
                    new NpgsqlParameter("@price", ctx.Message.Price)
                )
        );
    }
}
```

There, `Project` is a small helper function that creates a command from a given connection, sets the command type to `Text`, assigns the given SQL statement, and adds parameters to the command. It then returns the command, so it can be executed by the projector.

You can then register the projector as a subscription handler:

```csharp titlle="Program.cs"
builder.Services.AddSubscription<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(
    "ImportedBookingsProjections",
    builder => builder
        .UseCheckpointStore<PostgresCheckpointStore>()
        .AddEventHandler<ImportingBookingsProjector>();
);
```

:::note
You only need to explicitly specify the subscription checkpoint store with `UseCheckpointStore` if your application uses different checkpoint stores for different subscriptions.
At this moment, there is no way to use different checkpoint store options for each subscription in the same application, they will all use the same `PostgresCheckpointStoreOptions`.
:::

