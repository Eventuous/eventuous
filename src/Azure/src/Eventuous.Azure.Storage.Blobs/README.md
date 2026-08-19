# Eventuous Azure Blob Storage Projections

This package adds Azure Blob Storage projections to applications built with Eventuous. It allows you to project event store events to Azure Blob Storage as state objects, maintaining a separate state document for each event stream.

## Using projections

Create your own projection class that inherits from `BlobStorageProjector<T>` where `T` is your state type. The state type must be a class with a parameterless constructor.

Register event handlers using the `On<TEvent>` methods. When an event is received, the projector retrieves the current state blob (or creates a new state instance if the blob doesn't exist), applies the event to the state using the registered event handler, and uploads the updated state back to Blob Storage.

The class provides two constructors:

* `BlobStorageProjector(BlobContainerClient container, ...` where the container client is passed directly
* `BlobStorageProjector(BlobServiceClient serviceClient, string containerName, ...` where the service client is set up by Azure DI and the container name is set by the projection

The blob container must exist before the projector handles events; the projector doesn't create it.

JSON serialization is configured via `BlobStorageProjectorOptions.JsonOptions`. When you keep serializer options in ASP.NET Core DI, pass them on: `new BlobStorageProjectorOptions { JsonOptions = options.Value }`.

By default, the blob ID is extracted from the stream using `context.Stream.GetId()`. You can override this by providing a custom `getBlobId` function in the event registration:

```csharp
public class BookingProjection : BlobStorageProjector<BookingState> {
    public BookingProjection(BlobServiceClient client)
        : base(client, "bookings-container") {

        // Uses default blob ID from stream
        On<BookingImported>((state, evt) => {
            state.RoomId = evt.RoomId;
            state.CheckInDate = evt.CheckIn;
            return state;
        });

        // Custom blob ID using event data
        On<BookingPaymentRegistered>(
            (state, evt) => {
                state.PaidAmount += evt.AmountPaid;
                return state;
            },
            context => new ValueTask<string>($"custom-{context.Message.BookingId}")
        );
    }
}
```

## Projector options

The `BlobStorageProjectorOptions` class provides several configuration options for fine-tuning the projector behavior.

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `JsonOptions` | `JsonSerializerOptions?` | `null` (uses `JsonSerializerOptions.Web`) | JSON serializer options for state serialization/deserialization. Controls formatting, naming policies, etc. |
| `RaceRetries` | `int` | `0` | Number of retry attempts for optimistic concurrency conflicts. Increase when concurrent updates are likely. |
| `IdempotencyMode` | `IdempotencyMode` | `IdempotencyMode.None` | Controls duplicate message detection behavior. |

### Idempotency modes

The `IdempotencyMode` enum controls how the projector handles duplicate messages:

- **`None`** - No idempotency checks. Will process messages and updates blob, without
checking for duplicates.
- **`ByGlobalPosition`** - Skips processing if the existing blob has a global position set in
its metadata that indicates it has already been processed. The event global position must be greater than that stored in the blob.
This mode requires a subscription that provides real global positions, such as an all-stream subscription.
Do not use it with message broker subscriptions where the global position is always zero — the first event would store
position `0` and every subsequent event would be treated as a duplicate and silently ignored. Use `ByMessageId` instead.
- **`ByMessageId`** - Use this when building projections directly from integration events. Skips
processing if the message ID in the blob metadata matches that in the event.
Note, this means the idempotency is weaker as only the last message ID is checked. Older messages that are replayed will be processed as normal.

### Custom blob naming

By default, blob names are generated using `GetBlobName(string id)` which creates names in the format `{id}/{T}.json`, where `id` defaults to the stream ID from `context.Stream.GetId()`.

You can customize blob naming in two ways:

**1. Override the virtual methods globally for all events:**

```csharp
protected override string GetBlobName(string id, IMessageConsumeContext context) {
    // Use stream name and type in the path
    var streamName = context.Stream.ToString();
    return $"projections/{streamName}/{id}.json";
}

protected override string GetBlobName(string id) {
    return $"{id}/{typeof(T).Name}.json";
}
```

**2. Override blob ID per event handler using `getBlobId`:**

```csharp
On<BookingPaymentRegistered>(
    (state, evt) => {
        state.PaidAmount += evt.AmountPaid;
        return state;
    },
    // Custom blob ID for this specific event only
    context => new ValueTask<string>($"payments-{context.Message.BookingId}")
);
```

Note that `getBlobId` returns a blob _ID_, not a full blob name: the result is still passed to `GetBlobName`, so with the default naming the example above produces `payments-{id}/BookingState.json`. To change the full blob path, override `GetBlobName` as well.

Use per-event blob ID overrides when you need different events to target different blobs within the same projector, such as when the business identifier differs from the stream identifier.

## Features

- **Automatic state management** - Creates new state instances when blobs don't exist
- **Optimistic concurrency control** - Uses ETags for safe concurrent updates
- **Idempotency** - Prevents duplicate processing with configurable modes
- **Retry handling** - Automatic retries for race conditions
- **Flexible blob naming** - Customizable blob ID and naming conventions
- **Metadata storage** - Automatically stores stream info, positions, and message IDs

## Background

The projector stores each state as a separate blob in Azure Blob Storage. Each blob contains:
- The serialized state object (JSON by default)
- Metadata including stream name, message ID, stream position, and global position
- Content type set to `application/json`

This approach provides natural partitioning by stream and enables efficient state retrieval for individual streams.