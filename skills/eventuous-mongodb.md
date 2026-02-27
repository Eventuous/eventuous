# Eventuous MongoDB Projections

## NuGet Package

```
Eventuous.Projections.MongoDB
```

Depends on `MongoDB.Driver` and `Eventuous.Subscriptions`.

## MongoDB Client Setup

Register `IMongoDatabase` as a singleton. The projectors and checkpoint store both depend on it.

```csharp
// Do not hardcode credentials; use secret storage or environment variables
var settings = MongoClientSettings.FromConnectionString(configuration["MongoDB:ConnectionString"]!);
var database = new MongoClient(settings).GetDatabase("mydb");

services.AddSingleton(database);
```

## Document Base Types

All projected documents must inherit from `ProjectedDocument` (namespace `Eventuous.Projections.MongoDB.Tools`):

```csharp
public abstract record Document(string Id);

public abstract record ProjectedDocument(string Id) : Document(Id) {
    public ulong StreamPosition { get; init; }
    public ulong Position       { get; init; }
}
```

`StreamPosition` and `Position` are set automatically by the projector framework on every update/insert.

Define your document as a record inheriting `ProjectedDocument`:

```csharp
public record BookingDocument(string Id) : ProjectedDocument(Id) {
    public string GuestId      { get; init; } = null!;
    public string RoomId       { get; init; } = null!;
    public float  BookingPrice { get; init; }
    public float  Outstanding  { get; init; }
    public bool   Paid         { get; init; }
}
```

## Collection Naming Convention

`MongoCollectionName.For<T>()` derives the collection name by stripping these suffixes from the type name: `"Document"`, `"Entity"`, `"View"`, `"Projection"`, `"ProjectionDocument"`, `"ProjectionEntity"`. The result is used as the MongoDB collection name.

- `BookingDocument` -> collection `"Booking"`
- `MyBookings` -> collection `"MyBookings"`

Override by passing `MongoProjectionOptions<T>` to the base constructor.

## MongoProjector<T> Base Class

Inherit from `MongoProjector<T>` and register event handlers in the constructor:

```csharp
public class BookingStateProjection : MongoProjector<BookingDocument> {
    public BookingStateProjection(IMongoDatabase database) : base(database) {
        // Register handlers here using On<TEvent>(...) methods
    }
}
```

Constructor overloads:
- `MongoProjector(IMongoDatabase database, MongoProjectionOptions<T>? options = null, ITypeMapper? typeMap = null)`

The projector exposes `protected IMongoCollection<T> Collection` for direct access if needed.

## Handler Registration Patterns

### 1. Operation Builder (preferred)

The fluent `On<TEvent>(b => b.{Operation}.{IdOrFilter}.{Update/Document})` pattern:

```csharp
On<RoomBooked>(b => b
    .UpdateOne
    .DefaultId()                              // ID from stream name
    .Update((evt, update) =>                  // evt is the event message
        update.Set(x => x.RoomId, evt.RoomId)
    )
);
```

### 2. Shorthand: ID from stream + update delegate

```csharp
On<RoomBooked>(
    stream => stream.GetId(),                 // GetDocumentIdFromStream
    (ctx, update) =>                          // BuildUpdate<TEvent, T>
        update.Set(x => x.RoomId, ctx.Message.RoomId)
);
```

### 3. Shorthand: ID from event + update delegate

```csharp
On<RoomBooked>(
    evt => evt.GuestId,                       // GetDocumentIdFromEvent<TEvent>
    (ctx, update) =>
        update.Set(x => x.GuestId, ctx.Message.GuestId)
);
```

### 4. Shorthand: Custom filter + update delegate

```csharp
On<BookingCancelled>(
    (ctx, filter) =>                          // BuildFilter<TEvent, T>
        filter.Eq(x => x.GuestId, ctx.Message.GuestId),
    (ctx, update) =>
        update.Set(x => x.Cancelled, true)
);
```

### 5. Async variants

Use `OnAsync<TEvent>(...)` for async update builders (same overload shapes as sync but returning `ValueTask<UpdateDefinition<T>>`).

### 6. Raw handler

```csharp
On<RoomBooked>(handler);
// where handler is: ProjectTypedEvent<T, TEvent>
// signature: MessageConsumeContext<TEvent> -> ValueTask<MongoProjectOperation<T>>
```

## Operation Builders in Detail

Access via the fluent builder lambda `b => b.{Operation}`:

| Builder        | MongoDB Operation   |
|---------------|---------------------|
| `b.UpdateOne`  | `UpdateOneAsync`    |
| `b.UpdateMany` | `UpdateManyAsync`   |
| `b.InsertOne`  | `InsertOneAsync`    |
| `b.InsertMany` | `InsertManyAsync`   |
| `b.DeleteOne`  | `DeleteOneAsync`    |
| `b.DeleteMany` | `DeleteManyAsync`   |
| `b.Bulk`       | `BulkWriteAsync`    |

### UpdateOne / UpdateMany

```csharp
On<MyEvent>(b => b
    .UpdateOne
    .DefaultId()                                    // ID = stream.GetId()
    .Update((evt, update) => update.Set(...))       // from event message
);

On<MyEvent>(b => b
    .UpdateOne
    .Id(ctx => ctx.Message.SomeId)                  // custom ID from context
    .UpdateFromContext((ctx, update) => update.Set(...))  // from full context
);

On<MyEvent>(b => b
    .UpdateOne
    .IdFromStream(stream => stream.GetId())         // ID from StreamName
    .Update((evt, update) => update.Set(...))
);

On<MyEvent>(b => b
    .UpdateOne
    .Filter((ctx, filter) => filter.Eq(...))        // custom filter
    .UpdateFromContext((ctx, update) => update.Set(...))
);
```

Update defaults: `IsUpsert = true`. Override with `.Configure(opts => opts.IsUpsert = false)`.

### InsertOne

```csharp
On<MyEvent>(b => b
    .InsertOne
    .Document((stream, evt) => new MyDocument(stream.GetId()) {
        Name = evt.Name
    })
);

// Or from full context:
On<MyEvent>(b => b
    .InsertOne
    .Document(ctx => new MyDocument(ctx.Stream.GetId()) {
        Name = ctx.Message.Name
    })
);
```

### DeleteOne

```csharp
On<MyEvent>(b => b
    .DeleteOne
    .DefaultId()                    // ID = stream.GetId()
);

On<MyEvent>(b => b
    .DeleteOne
    .Id(ctx => ctx.Message.ItemId)  // custom ID
);

On<MyEvent>(b => b
    .DeleteOne
    .Filter((ctx, filter) => filter.Eq(x => x.SomeField, ctx.Message.Value))
);
```

### Bulk

Combine multiple operations in a single `BulkWriteAsync`:

```csharp
On<MyEvent>(b => b
    .Bulk
    .AddOperation(ops => ops
        .UpdateOne
        .Id(ctx => ctx.Message.Id1)
        .Update((evt, update) => update.Set(x => x.Field1, evt.Value1))
    )
    .AddOperation(ops => ops
        .DeleteOne
        .Id(ctx => ctx.Message.Id2)
    )
);
```

## MongoCheckpointStore

Stores subscription checkpoint positions in a MongoDB collection (default: `"checkpoint"`).

### Registration

```csharp
services.AddCheckpointStore<MongoCheckpointStore>();
```

Or as part of a subscription builder:

```csharp
services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
    "MyProjections",
    builder => builder
        .UseCheckpointStore<MongoCheckpointStore>()
        .AddEventHandler<BookingStateProjection>()
);
```

### Options

Configure via `MongoCheckpointStoreOptions`:

```csharp
services.Configure<MongoCheckpointStoreOptions>(opts => {
    opts.CollectionName  = "checkpoint";     // default: "checkpoint"
    opts.BatchSize       = 10;               // default: 1
    opts.BatchIntervalSec = 5;               // default: 5
});
```

Batching reduces write frequency. When both `BatchSize` and `BatchIntervalSec` are set, checkpoints are flushed when either threshold is reached.

## Querying Projected Documents

`IMongoDatabase` extension methods (from `Eventuous.Projections.MongoDB.Tools`) for reading documents:

```csharp
var doc = await database.LoadDocument<BookingDocument>(id, cancellationToken);
var exists = await database.DocumentExists<BookingDocument>(id, cancellationToken);
var docs = await database.LoadDocuments<BookingDocument>(ids, cancellationToken);
var queryable = database.AsQueryable<BookingDocument>();
```

## Complete Example

```csharp
// --- Document ---
public record OrderDocument(string Id) : ProjectedDocument(Id) {
    public string   CustomerId { get; init; } = null!;
    public decimal  Total      { get; init; }
    public string   Status     { get; init; } = null!;
    public List<OrderLine> Lines { get; init; } = [];

    public record OrderLine(string ProductId, int Quantity, decimal Price);
}

// --- Projector ---
public class OrderProjection : MongoProjector<OrderDocument> {
    public OrderProjection(IMongoDatabase database) : base(database) {

        // Insert a new document when an order is created
        On<OrderCreated>(b => b
            .InsertOne
            .Document((stream, evt) => new OrderDocument(stream.GetId()) {
                CustomerId = evt.CustomerId,
                Total      = 0,
                Status     = "Created"
            })
        );

        // Update using DefaultId (from stream name)
        On<OrderLineAdded>(b => b
            .UpdateOne
            .DefaultId()
            .UpdateFromContext((ctx, update) => {
                var evt = ctx.Message;
                return update
                    .Push(x => x.Lines, new OrderDocument.OrderLine(evt.ProductId, evt.Quantity, evt.Price))
                    .Inc(x => x.Total, evt.Quantity * evt.Price);
            })
        );

        // Simple update with shorthand
        On<OrderConfirmed>(b => b
            .UpdateOne
            .DefaultId()
            .Update((_, update) => update.Set(x => x.Status, "Confirmed"))
        );

        // Delete a document
        On<OrderDeleted>(b => b
            .DeleteOne
            .DefaultId()
        );
    }
}

// --- Registration ---
services.AddSingleton(database);

services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
    "OrderProjections",
    builder => builder
        .UseCheckpointStore<MongoCheckpointStore>()
        .AddEventHandler<OrderProjection>()
        .WithPartitioningByStream(2)
);
```

## Key Source Files

- Projector base class: `src/Mongo/src/Eventuous.Projections.MongoDB/MongoProjector.cs`
- Operation builder: `src/Mongo/src/Eventuous.Projections.MongoDB/MongoOperationBuilder.cs`
- Update/Insert/Delete builders: `src/Mongo/src/Eventuous.Projections.MongoDB/Operations/`
- Checkpoint store: `src/Mongo/src/Eventuous.Projections.MongoDB/MongoCheckpointStore.cs`
- Document base types: `src/Mongo/src/Eventuous.Projections.MongoDB/Tools/Document.cs`
- Collection extensions: `src/Mongo/src/Eventuous.Projections.MongoDB/Tools/MongoCollectionExtensions.cs`
- Database extensions: `src/Mongo/src/Eventuous.Projections.MongoDB/Tools/MongoDatabaseExtensions.cs`
- Delegate types: `src/Mongo/src/Eventuous.Projections.MongoDB/Functional.cs`
- Sample projections: `samples/kurrentdb/Bookings/Application/Queries/`
