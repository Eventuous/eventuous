---
title: "PostgreSQL"
description: "Supported PostgreSQL infrastructure"
sidebar_position: 3
---

PostgreSQL is a powerful, open source object-relational database system with over 30 years of active development that has earned it a strong reputation for reliability, feature robustness, and performance. [source](https://www.postgresql.org/).

Eventuous supports Postgres as an event store, and also allows subscribing to the global event log and to individual streams using catch-up subscriptions.

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

Usually, you just need to register the aggregate store that uses the Postgres event store. For that to work, you'd also need to register a Postgres connection factory, which is used to create connections to the database.

```csharp titlle="Program.cs"
// Local connection factory function
NpgsqlConnection GetConnection() => new(connectionString);

builder.Services.AddSingleton((GetPostgresConnection)GetConnection);
builder.Services.AddAggregateStore<PostgresStore>();
```

For the newer NpgSql driver (v7+), you can use the `NpgsqlDataSourceBuilder`:

```csharp titlle="Program.cs"
var ds = new NpgsqlDataSourceBuilder(connectionString).Build();
NpgsqlConnection GetConnection() => ds.CreateConnection();
builder.Services.AddSingleton(GetConnection());
builder.Services.AddAggregateStore<PostgresStore>();
```

You can also override the default schema by configuring the store options:

```json title="appsettings.json"
{
  "PostgresStore": {
    "Schema": "my-schema"
  }
}
```

```csharp titlle="Program.cs"
builder.Services.Configure<PostgresStoreOptions>(
    builder.Configuration.GetSection("PostgresStore")
);
```

When that's done, Eventuous would persist aggregates in Postgres when you use the [command service](../../application/app-service).

At this moment, the Postgres event store implementation doesn't support stream truncation.

## Subscriptions

Eventuous supports two types of subscriptions to Postgres: global and stream. The global subscription is a catch-up subscription, which means that it reads all events from the beginning of the event log. The stream subscription is also a catch-up subscription, but it only reads events from a specific stream.

Both subscription types use continuous polling to check for new events. We don't use the notifications feature of Postgres database.

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

### Checkpoint store

Catch-up subscriptions need a [checkpoint](../../subscriptions/checkpoint). You can register the checkpoint store using `AddCheckpointStore<T>`, and it will be used for all subscriptions in the application.

Remember to store the checkpoint in the same database as the read model. For example, if you use Postgres as an event store, and project events to read models in MongoDB, you need to use the `MongoCheckpointStore`. Eventuous also has a checkpoint store implementation for Postgres (`PostgresCheckpointStore`), which you can use if you project events to Postgres.

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
    public ImportingBookingsProjector(GetPostgresConnection getConnection) : base(getConnection) {
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

There, `Project` is a small helper function that creates a command from a given connection, sets the command type to `Text`, assigns the given SQL statement, and adds the given parameters to the command. It then returns the command, so it can be executed by the projector.

You can then register the projector as a subscription handler:

```csharp titlle="Program.cs"
builder.Services.AddSubscription<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(
    "ImportedBookingsProjections",
    builder => builder
        .UseCheckpointStore<PostgresCheckpointStore>()
        .AddEventHandler<ImportingBookingsProjector>();
);
```

Note that the `insert` operation in the projection is not idempotent, so if the event is processed twice because there was a failure, the projector will throw an exception. It would not be an issue when the subscription uses the default setting that tells it not to stop when the handler fails. If you want to ensure that failures force the subscription to throw, you can change the subscription option `ThroOnError` to `true`, and make the operation idempotent by using "insert or update".
