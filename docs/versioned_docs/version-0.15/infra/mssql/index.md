---
title: "Microsoft SQL Server"
description: "Supported Microsoft SQL Server infrastructure"
sidebar_position: 4
---

Microsoft SQL Server is a popular choice for storing queryable application data. Many organizations that use .NET as their preferred stack also use SQL Server.

Eventuous supports SQL Server as an event store and also allows subscribing to the global event log and to individual streams using catch-up subscriptions.

## Data model

Eventuous uses a single table to store events. The table name is `Messages`. In addition, another table called `Streams` is used to control the stream existence, and store the last event number for each stream. In the `Messages` table, events and metadata are stored as `NVARCHAR(max)` columns. The table schema is as follows:

```sql
MessageId      UNIQUEIDENTIFIER      NOT NULL,
MessageType    NVARCHAR(128)         NOT NULL,
StreamId       INT                   NOT NULL,
StreamPosition INT                   NOT NULL,
GlobalPosition BIGINT IDENTITY (0,1) NOT NULL,
JsonData       NVARCHAR(max)         NOT NULL,
JsonMetadata   NVARCHAR(max),
Created        DATETIME2
```

For subscriptions, Eventuous adds a table called `Checkpoints` that stores the last processed event number for each subscription. It is then used by the checkpoint store implementation for SQL Server.

## Event persistence

Before using SQL Server as an event store, you need to register the SQL Server-based event store implementation.
For that to work, you'd also need to register an SQL Sever connection detail used to create connections to the database.
Eventuous provides a few overloads for `AddEventuousSqlServer` registration extension to do that.

One way to register the connection details is to provide a connection string and, optionally, the schema name:

```csharp titlle="Program.cs"
builder.Services.AddEventuousSqlServer(connectionString, "mySchema", true);
```

If the schema name is not provided, the default schema name (`eventuous`) will be used.

Another way to register the connection details is by using configuration options. For example, you can add the following to the settings file:

```json title="appSettings.json"
{
  "SqlServerStore": {
    "Schema": "mySchema",
    "ConnectionString": "Server=127.0.0.1,57456;Database=master;User Id=sa;Password=password;TrustServerCertificate=True",
    "InitializeDatabase": true
  }
}
```

Then, use the configuration section to register the connection details:

```csharp titlle="Program.cs"
builder.Services.AddEventuousSqlServer(
    builder.Configuration.GetSection("SqlServerStore")
);
```

The `InitializeDatabase` setting tells Eventuous if it needs to create the schema. If you create the schema in a separate migration application, set this setting to `false`. If the schema cannot be found, and the `InitializeDatabase` setting is set to `false`, the application will fail to start.

Next, you need to register the SQL Server event store:

```csharp
builder.Services.AddEventStore<SqlServerStore>();
```

When that's done, Eventuous will use SQL Server for persistence in command services.

:::note
At this moment, the SQL Server event store implementation doesn't support stream truncation.
:::

## Subscriptions

Eventuous supports two types of subscriptions to SQL Server: global and stream. The global subscription is a catch-up subscription, which means that it reads all events from the beginning of the event log. The stream subscription is also a catch-up subscription, but it only reads events from a specific stream.

Both subscription types use continuous polling to check for new events.

### Registering subscriptions

Registering a global log subscription is similar to [EventStoreDB](../esdb/index.md#all-stream-subscription). The only difference is the subscription and the options types:

```csharp titlle="Program.cs"
builder.Services.AddSubscription<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions>(
    "BookingsProjections",
    builder => builder
        .AddEventHandler<BookingStateProjection>()
        .AddEventHandler<MyBookingsProjection>();
);
```

When you register a subscription to a single stream, you need to configure the subscription options to specify the stream name:

```csharp titlle="Program.cs"
builder.Services.AddSubscription<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions>(
    "StreamSubscription",
    builder => builder
        .Configure(x => x.StreamName = "my-stream")
        .AddEventHandler<StreamSubscriptionHander>()
);
```

As subscriptions use the SQL Server connection details for opening the connection, there's no need to register additional dependencies apart from calling `AddEventuousSqlServer` as described in [event persistence](#event-persistence) section above.

### Checkpoint store

Catch-up subscriptions need a [checkpoint](../../subscriptions/checkpoint). You can register the checkpoint store using `AddCheckpointStore<T>`, and it will be used for all subscriptions in the application.

Remember to store the checkpoint in the same database as the read model.
For example, if you use Postgres as an event store, and project events to read models in MongoDB, you need to use the `MongoCheckpointStore`.
Eventuous also has a checkpoint store implementation for SQL Server (`SqlServerCheckpointStore`), which you can use if you project events to SQL Server.

When using the SQL Server checkpoint store, you can register it using a dedicated extension function:

```csharp
builder.Services.AddSqlServerCheckpointStore();
```

This registration function will use the schema name provided when you register the connection details using `AddEventuousSqlServer`.
If checkpoints need to be stored in another SQL Server instance, database, or schema, you'd need to register an instance of `SqlServerCheckpointStoreOptions` to configure that:

```csharp
builder.Services.AddSingleton(
    new SqlServerCheckpointStoreOptions(anotherConnectionString, anotherSchema)
);
builder.Services.AddSqlServerCheckpointStore();
```

When you also plan to use a different SQL Server database (not the one where events are stored) for both checkpoints and read models,
you can override the `SqlServerConnectionOptions` that are used by both checkpoint store and projectors (see below). It _must_ be done before calling `AddEventuousSqlServer`.

```csharp
builder.Services.AddSingleton(
    new SqlServerCheckpointStoreOptions(anotherConnectionString, anotherSchema)
);
builder.Services.AddSqlServerCheckpointStore();
builder.Services.AddEventuousSqlServer(
    builder.Configuration.GetSection("SqlServerStore")
);
```

Connection details from `SqlServerConnectionOptions` are used by default by the checkpoint store and projectors.

### Projections

You can use SQL Server both as an event store and as a read model store. In that case, you can use the same connection factory for both the event store, the checkpoint store, and the projector.

Eventuous provides a simple projector base class, which allows you to emit SQL statements for the events you want to project, and the projector will execute them.

Consider the following table schema for the query model:

```sql
IF OBJECT_ID('__schema__.Bookings', 'U') IS NULL
BEGIN
  CREATE TABLE __schema__.Bookings (
    BookingId VARCHAR(1000) NOT NULL PRIMARY KEY,
    CheckinDate DATETIME2,
    Price NUMERIC(10,2)
  );
END
```

You can project the `BookingImported` event to this table using a simple projector:

```csharp title="ImportingBookingsProjector.cs"
public class ImportingBookingsProjector : SqlServerProjector {
    public ImportingBookingsProjector(SqlServerConnectionOptions options) : base(options) {
        var insert = $"""
                      INSERT INTO {schemaInfo.Schema}.Bookings 
                      (BookingId, CheckinDate, Price) 
                      values (@BookingId, @CheckinDate, @Price)
                      """;

        On<BookingEvents.BookingImported>(
            (connection, ctx) =>
                Project(
                    connection,
                    insert,
                    new SqlParameter("@BookingId", ctx.Stream.GetId()),
                    new SqlParameter("@CheckinDate", ctx.Message.CheckIn.ToDateTimeUnspecified()),
                    new SqlParameter("@Price", ctx.Message.Price)
                )
        );
    }
}
```

There, `Project` is a small helper function that creates a command from a given connection, sets the command type to `Text`, assigns the given SQL statement, and adds parameters to the command. It then returns the command, so it can be executed by the projector.

You can then register the projector as a subscription handler:

```csharp titlle="Program.cs"
builder.Services.AddSubscription<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions>(
    "ImportedBookingsProjections",
    builder => builder
        .UseCheckpointStore<SqlServerCheckpointStore>()
        .AddEventHandler<ImportingBookingsProjector>();
);
```

:::note
You only need to explicitly specify the subscription checkpoint store with `UseCheckpointStore` if your application uses different checkpoint stores for different subscriptions.
At this moment, there is no way to use different checkpoint store options for each subscription in the same application, they will all use the same `SqlServerCheckpointStoreOptions` or `SqlServerConnectionOptions`.
:::

Note that the `insert` operation in the projection is not idempotent, so if the event is processed twice because there was a failure, the projector will throw an exception. It would not be an issue when the subscription uses the default setting that tells it not to stop when the handler fails. If you want to ensure that failures force the subscription to throw, you can change the subscription option `ThroOnError` to `true`, and make the operation idempotent by using "insert or update".
Microsoft SQL Server is a popular choice for storing queryable application data. Many organizations that use .NET as their preferred stack also use SQL Server.

Eventuous supports SQL Server as an event store and also allows subscribing to the global event log and to individual streams using catch-up subscriptions.

## Data model

Eventuous uses a single table to store events. The table name is `Messages`. In addition, another table called `Streams` is used to control the stream existence, and store the last event number for each stream. In the `Messages` table, events and metadata are stored as `NVARCHAR(max)` columns. The table schema is as follows:

```sql
MessageId      UNIQUEIDENTIFIER      NOT NULL,
MessageType    NVARCHAR(128)         NOT NULL,
StreamId       INT                   NOT NULL,
StreamPosition INT                   NOT NULL,
GlobalPosition BIGINT IDENTITY (0,1) NOT NULL,
JsonData       NVARCHAR(max)         NOT NULL,
JsonMetadata   NVARCHAR(max),
Created        DATETIME2
```

For subscriptions, Eventuous adds a table called `Checkpoints` that stores the last processed event number for each subscription. It is then used by the checkpoint store implementation for SQL Server.

## Event persistence

Before using SQL Server as an event store, you need to register the SQL Server-based event store implementation.
For that to work, you'd also need to register an SQL Sever connection detail used to create connections to the database.
Eventuous provides a few overloads for `AddEventuousSqlServer` registration extension to do that.

One way to register the connection details is to provide a connection string and, optionally, the schema name:

```csharp titlle="Program.cs"
builder.Services.AddEventuousSqlServer(connectionString, "mySchema", true);
```

If the schema name is not provided, the default schema name (`eventuous`) will be used.

Another way to register the connection details is by using configuration options. For example, you can add the following to the settings file:

```json title="appSettings.json"
{
  "SqlServerStore": {
    "Schema": "mySchema",
    "ConnectionString": "Server=127.0.0.1,57456;Database=master;User Id=sa;Password=password;TrustServerCertificate=True",
    "InitializeDatabase": true
  }
}
```

Then, use the configuration section to register the connection details:

```csharp titlle="Program.cs"
builder.Services.AddEventuousSqlServer(
    builder.Configuration.GetSection("SqlServerStore")
);
```

The `InitializeDatabase` setting tells Eventuous if it needs to create the schema. If you create the schema in a separate migration application, set this setting to `false`. If the schema cannot be found, and the `InitializeDatabase` setting is set to `false`, the application will fail to start.

Next, you need to register the SQL Server event store:

```csharp
builder.Services.AddEventStore<SqlServerStore>();
```

When that's done, Eventuous will use SQL Server for persistence in command services.

<<<<<<< HEAD
=======
:::note
At this moment, the SQL Server event store implementation doesn't support stream truncation.
:::

>>>>>>> refs/heads/further-cleanup-services
## Subscriptions

Eventuous supports two types of subscriptions to SQL Server: global and stream. The global subscription is a catch-up subscription, which means that it reads all events from the beginning of the event log. The stream subscription is also a catch-up subscription, but it only reads events from a specific stream.

Both subscription types use continuous polling to check for new events.

### Registering subscriptions

Registering a global log subscription is similar to [EventStoreDB](../esdb/index.md#all-stream-subscription). The only difference is the subscription and the options types:

```csharp titlle="Program.cs"
builder.Services.AddSubscription<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions>(
    "BookingsProjections",
    builder => builder
        .AddEventHandler<BookingStateProjection>()
        .AddEventHandler<MyBookingsProjection>();
);
```

When you register a subscription to a single stream, you need to configure the subscription options to specify the stream name:

```csharp titlle="Program.cs"
builder.Services.AddSubscription<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions>(
    "StreamSubscription",
    builder => builder
        .Configure(x => x.StreamName = "my-stream")
        .AddEventHandler<StreamSubscriptionHander>()
);
```

As subscriptions use the SQL Server connection details for opening the connection, there's no need to register additional dependencies apart from calling `AddEventuousSqlServer` as described in [event persistence](#event-persistence) section above.

### Checkpoint store

Catch-up subscriptions need a [checkpoint](../../subscriptions/checkpoint). You can register the checkpoint store using `AddCheckpointStore<T>`, and it will be used for all subscriptions in the application.

Remember to store the checkpoint in the same database as the read model.
For example, if you use Postgres as an event store, and project events to read models in MongoDB, you need to use the `MongoCheckpointStore`.
Eventuous also has a checkpoint store implementation for SQL Server (`SqlServerCheckpointStore`), which you can use if you project events to SQL Server.

When using the SQL Server checkpoint store, you can register it using a dedicated extension function:

```csharp
builder.Services.AddSqlServerCheckpointStore();
```

This registration function will use the schema name provided when you register the connection details using `AddEventuousSqlServer`.
If checkpoints need to be stored in another SQL Server instance, database, or schema, you'd need to register an instance of `SqlServerCheckpointStoreOptions` to configure that:

```csharp
builder.Services.AddSingleton(
    new SqlServerCheckpointStoreOptions(anotherConnectionString, anotherSchema)
);
builder.Services.AddSqlServerCheckpointStore();
```

When you also plan to use a different SQL Server database (not the one where events are stored) for both checkpoints and read models,
you can override the `SqlServerConnectionOptions` that are used by both checkpoint store and projectors (see below). It _must_ be done before calling `AddEventuousSqlServer`.

```csharp
builder.Services.AddSingleton(
    new SqlServerCheckpointStoreOptions(anotherConnectionString, anotherSchema)
);
builder.Services.AddSqlServerCheckpointStore();
builder.Services.AddEventuousSqlServer(
    builder.Configuration.GetSection("SqlServerStore")
);
```

Connection details from `SqlServerConnectionOptions` are used by default by the checkpoint store and projectors.

### Projections

You can use SQL Server both as an event store and as a read model store. In that case, you can use the same connection factory for both the event store, the checkpoint store, and the projector.

Eventuous provides a simple projector base class, which allows you to emit SQL statements for the events you want to project, and the projector will execute them.

Consider the following table schema for the query model:

```sql
IF OBJECT_ID('__schema__.Bookings', 'U') IS NULL
BEGIN
  CREATE TABLE __schema__.Bookings (
    BookingId VARCHAR(1000) NOT NULL PRIMARY KEY,
    CheckinDate DATETIME2,
    Price NUMERIC(10,2)
  );
END
```

You can project the `BookingImported` event to this table using a simple projector:

```csharp title="ImportingBookingsProjector.cs"
public class ImportingBookingsProjector : SqlServerProjector {
    public ImportingBookingsProjector(SqlServerConnectionOptions options) : base(options) {
        var insert = $"""
                      INSERT INTO {schemaInfo.Schema}.Bookings 
                      (BookingId, CheckinDate, Price) 
                      values (@BookingId, @CheckinDate, @Price)
                      """;

        On<BookingEvents.BookingImported>(
            (connection, ctx) =>
                Project(
                    connection,
                    insert,
                    new SqlParameter("@BookingId", ctx.Stream.GetId()),
                    new SqlParameter("@CheckinDate", ctx.Message.CheckIn.ToDateTimeUnspecified()),
                    new SqlParameter("@Price", ctx.Message.Price)
                )
        );
    }
}
```

There, `Project` is a small helper function that creates a command from a given connection, sets the command type to `Text`, assigns the given SQL statement, and adds parameters to the command. It then returns the command, so it can be executed by the projector.

You can then register the projector as a subscription handler:

```csharp titlle="Program.cs"
builder.Services.AddSubscription<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions>(
    "ImportedBookingsProjections",
    builder => builder
        .UseCheckpointStore<SqlServerCheckpointStore>()
        .AddEventHandler<ImportingBookingsProjector>();
);
```

:::note
You only need to explicitly specify the subscription checkpoint store with `UseCheckpointStore` if your application uses different checkpoint stores for different subscriptions.
At this moment, there is no way to use different checkpoint store options for each subscription in the same application, they will all use the same `SqlServerCheckpointStoreOptions` or `SqlServerConnectionOptions`.
:::

Note that the `insert` operation in the projection is not idempotent, so if the event is processed twice because there was a failure, the projector will throw an exception. It would not be an issue when the subscription uses the default setting that tells it not to stop when the handler fails. If you want to ensure that failures force the subscription to throw, you can change the subscription option `ThroOnError` to `true`, and make the operation idempotent by using "insert or update".
