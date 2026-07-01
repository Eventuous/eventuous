# Eventuous Azure Blob Storage Projections

This package adds Azure Blob Storage projections to applications built with Eventuous. It allows you to project event store events to Azure Blob Storage as state objects, maintaining a separate state document for each event stream.

## Using projections

Create your own projection class that inherits from `StorageBlobsProjector<T>` where `T` is your state type. The state type must be a class with a parameterless constructor.

Register event handlers using the `On<TEvent>` methods. When an event is received, the projector retrieves the current state blob (or creates a new state instance if the blob doesn't exist), applies the event to the state using the registered event handler, and uploads the updated state back to Blob Storage.

By default, the blob ID is extracted from the stream using `context.Stream.GetId()`. You can override this by providing a custom `getBlobId` function in the event registration:

```csharp
public class BookingProjection : StorageBlobsProjector<BookingState> {
    public BookingProjection(BlobContainerClient containerClient)
        : base(containerClient) {
        
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

Use the constructor that accepts a `BlobContainerClient`, or use the one that accepts a `BlobServiceClient` and container name:

```csharp
// Using BlobServiceClient and container name
public class BookingProjection : StorageBlobsProjector<BookingState> {
    public BookingProjection(BlobServiceClient serviceClient)
        : base(serviceClient, "bookings-container") {
        // Event handlers...
    }
}
```

## Projector options

The `StorageBlobProjectorOptions<T>` class provides several configuration options for fine-tuning the projector behavior.

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `JsonOptions` | `JsonSerializerOptions?` | `null` (uses `JsonSerializerOptions.Web`) | JSON serializer options for state serialization/deserialization. Controls formatting, naming policies, etc. |
| `Deserialize` | `Func<BinaryData, T>?` | `null` (uses JSON deserialization) | Custom function to deserialize blob content to state type. Override for custom deserialization logic. |
| `Serialize` | `Func<T, byte[]>?` | `null` (uses JSON serialization) | Custom function to serialize state to byte array. Override for custom serialization logic. |
| `RaceRetries` | `int` | `0` | Number of retry attempts for optimistic concurrency conflicts. Increase when concurrent updates are likely. |
| `IdempotencyMode` | `IdempotencyMode` | `IdempotencyMode.None` | Controls duplicate message detection behavior. |

### Idempotency modes

The `IdempotencyMode` enum controls how the projector handles duplicate messages:

- **`None`** - No idempotency checks. Always processes messages and updates blobs.
- **`ByGlobalPosition`** - Skips processing if existing blob has matching global position metadata.
- **`ByMessageId`** - Skips processing if existing blob has matching message ID metadata.

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
    context => new ValueTask<string>($"payments/{context.Message.BookingId}.json")
);
```

Use per-event blob ID overrides when you need different events to target different blob paths or naming conventions within the same projector, such as when the business identifier differs from the stream identifier.
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