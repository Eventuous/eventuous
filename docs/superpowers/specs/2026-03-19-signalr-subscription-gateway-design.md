# Eventuous SignalR Subscription Gateway

**Date:** 2026-03-19
**Status:** Draft

## Problem

Applications using Eventuous often need to push real-time event streams to remote clients — browser UIs, mobile apps, or other services. Today each application builds its own subscription-to-SignalR bridge: create an Eventuous `StreamSubscription`, serialize events, forward via hub, manage lifecycle per connection, handle reconnects. This is repetitive boilerplate with subtle correctness concerns (position tracking, cleanup on disconnect, reconnect resume).

## Goal

Provide two NuGet packages that handle the subscription-to-SignalR relay as reusable infrastructure:

- **`Eventuous.SignalR.Server`** — server-side gateway that bridges Eventuous subscriptions to SignalR, owns the wire contract source files
- **`Eventuous.SignalR.Client`** — client-side subscription API with auto-reconnect, links to contract sources from Server

The split ensures clients never pull in server-side subscription or event store dependencies. Wire contracts (`StreamEventEnvelope`, etc.) are source files owned by the Server project and compiled into the Client via `<Compile Include="...">` links — no shared binary package needed, since these are serialization DTOs that never cross assembly boundaries at runtime.

## Package Design

### Wire Contracts (source-shared)

These types live in `src/SignalR/src/Eventuous.SignalR.Server/Contracts/` and are linked into the Client project. They define the serialization format for the SignalR transport.

`StreamEventEnvelope` — the wire DTO sent over SignalR:

```csharp
public record StreamEventEnvelope {
    public required Guid EventId { get; init; }
    public required string Stream { get; init; }
    public required string EventType { get; init; }
    public required ulong StreamPosition { get; init; }
    public required ulong GlobalPosition { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string JsonPayload { get; init; }
    public string? JsonMetadata { get; init; }
}
```

The payload is pre-serialized JSON on the server side. This avoids polymorphic serialization issues (`StreamEvent.Payload` is `object?`) and lets the client deserialize with `TypeMap` when typed consumption is used.

**Distributed tracing:** Trace context flows through `JsonMetadata`, not as separate fields. The server's `BaseProducer` stamps `$traceId` and `$spanId` into the `ProducedMessage.Metadata` via `AddActivityTags`. The `RouteAndTransform` serializes this metadata into `JsonMetadata`. On the client, the `TypedStreamSubscription` deserializes `JsonMetadata` back into a `Metadata` dictionary, calls `GetTracingMeta()` to extract `TracingMeta(traceId, spanId)`, creates a parent `ActivityContext` via `ToActivityContext()`, and starts a consume `Activity` linked to the original trace. This mirrors exactly how normal Eventuous subscriptions restore trace context from event metadata — the SignalR transport is transparent to the tracing pipeline.

**Position semantics:** The envelope carries both `StreamPosition` (position within the subscribed stream) and `GlobalPosition` (position in the global commit log). For per-stream subscriptions, the client uses `StreamPosition` for resume and deduplication. `GlobalPosition` is included for consumers that need cross-stream ordering or may later subscribe to `$all`.

`StreamSubscriptionError` — error notification:

```csharp
public record StreamSubscriptionError {
    public required string Stream { get; init; }
    public required string Message { get; init; }
}
```

Hub method name constants:

```csharp
public static class SignalRSubscriptionMethods {
    // Server methods (client calls these)
    public const string Subscribe = "SubscribeToStream";
    public const string Unsubscribe = "UnsubscribeFromStream";

    // Client methods (server calls these)
    public const string StreamEvent = "StreamEvent";
    public const string StreamError = "StreamError";
}
```

### Eventuous.SignalR.Server

Server-side gateway that manages per-connection Eventuous subscriptions and forwards events over SignalR. Builds on the existing Eventuous Gateway pattern (`GatewayHandler` + `IProducer`).

**Dependencies:** Eventuous.SignalR.Server depends on Eventuous.Subscriptions, Eventuous.Gateway, Microsoft.AspNetCore.SignalR.Core

#### Reusing the Gateway Pattern

The existing Eventuous Gateway provides `GatewayHandler<TProduceOptions>` — a `BaseEventHandler` that takes events from a subscription, runs them through a `RouteAndTransform` function, and produces them via an `IProducer<TProduceOptions>`. The SignalR server reuses this directly:

- **`SignalRProducer`** implements `IProducer<SignalRProduceOptions>` — sends `StreamEventEnvelope` to a specific SignalR connection via `IHubContext`
- **`RouteAndTransform<SignalRProduceOptions>`** — serializes `IMessageConsumeContext` into a `StreamEventEnvelope`, wraps it in a `GatewayMessage` with the target stream and connection options
- **`GatewayHandler<SignalRProduceOptions>`** — the existing gateway handler, used as-is

```csharp
public record SignalRProduceOptions(string ConnectionId);

public class SignalRProducer<THub>(IHubContext<THub> hubContext)
    : BaseProducer<SignalRProduceOptions>(new ProducerTracingOptions { ProducerName = "signalr" })
    where THub : Hub {

    protected override async Task ProduceMessages(
        StreamName stream,
        IEnumerable<ProducedMessage> messages,
        SignalRProduceOptions? options,
        CancellationToken ct = default
    ) {
        var client = hubContext.Clients.Client(options!.ConnectionId);
        foreach (var msg in messages) {
            await client.SendAsync(
                SignalRSubscriptionMethods.StreamEvent,
                msg.Message, // StreamEventEnvelope — metadata already has $traceId/$spanId
                ct
            );
        }
    }
}
```

By extending `BaseProducer`, the producer gets automatic OpenTelemetry `Activity` creation via `ProducerActivity.Start`. The `BaseProducer.Produce` method creates a produce activity, stamps trace context (`$traceId`, `$spanId`) into `ProducedMessage.Metadata` via `AddActivityTags`, then calls `ProduceMessages`. The `RouteAndTransform` serializes this enriched metadata into `StreamEventEnvelope.JsonMetadata`, so trace context flows through the existing metadata pipeline — no special handling needed in the producer.

The `RouteAndTransform` serializes events to envelopes. Metadata from the consume context (which includes trace context from the original event) is serialized into `JsonMetadata`. The `BaseProducer` later enriches the `ProducedMessage.Metadata` with produce-side trace tags, but the `RouteAndTransform` runs first — so the transform serializes the original event metadata. The produce-side trace context is what `BaseProducer` adds to `ProducedMessage.Metadata`, which then gets passed to `ProduceMessages`. To ensure the produce-side trace IDs flow to the client, the `SignalRProducer.ProduceMessages` should re-serialize metadata from `msg.Metadata` (the enriched one) rather than using the pre-built envelope's `JsonMetadata`.

Alternatively, the simpler approach: the `RouteAndTransform` builds the envelope with `JsonMetadata` from `ctx.Metadata` (which already has `$traceId`/`$spanId` if the event was originally stored with trace context). For events that don't have trace context in their metadata, the client simply won't have a parent trace — which is acceptable.

```csharp
RouteAndTransform<SignalRProduceOptions> CreateTransform(
    string connectionId, string stream, IEventSerializer serializer
) => ctx => {
    var result = serializer.SerializeEvent(ctx.Message!);
    var envelope = new StreamEventEnvelope {
        EventId        = Guid.Parse(ctx.MessageId),
        Stream         = stream,
        EventType      = ctx.MessageType,
        StreamPosition = ctx.StreamPosition,
        GlobalPosition = ctx.GlobalPosition,
        Timestamp      = ctx.Created,
        JsonPayload    = Encoding.UTF8.GetString(result.Payload),
        JsonMetadata   = ctx.Metadata is { Count: > 0 }
            ? JsonSerializer.Serialize(ctx.Metadata.ToDictionary(kv => kv.Key, kv => kv.Value))
            : null
    };
    return ValueTask.FromResult(new[] {
        new GatewayMessage<SignalRProduceOptions>(
            new StreamName(stream), envelope, ctx.Metadata, new SignalRProduceOptions(connectionId)
        )
    });
};
```

Note: `GatewayMessage.Metadata` is passed through so `GatewayMetaHelper.GetMeta` can propagate causation IDs. The `GatewayHandler` uses this metadata when constructing the `ProducedMessage`, and `BaseProducer` enriches it with produce-side trace context.

**Event serialization note:** `IEventSerializer.SerializeEvent` returns `SerializationResult` containing `byte[] Payload`. The transform converts UTF-8 bytes to a string for the envelope. This involves a deserialize-then-reserialize round-trip (the subscription already deserialized the event). This is acceptable — the alternative (raw bytes from the store) isn't available on `IMessageConsumeContext`.

#### SubscriptionGateway

Manages dynamic per-connection subscription lifecycle. Uses `IHubContext<THub>` (singleton-safe) rather than `IHubClients` (hub-invocation-scoped).

```csharp
public class SubscriptionGateway<THub> : IAsyncDisposable where THub : Hub {
    public SubscriptionGateway(
        IHubContext<THub> hubContext,
        SubscriptionFactory subscriptionFactory,
        IEventSerializer eventSerializer,
        ILoggerFactory loggerFactory
    );

    /// Subscribe a connection to a stream, starting from the given position.
    Task SubscribeAsync(string connectionId, string stream, ulong? fromPosition, CancellationToken ct = default);

    /// Unsubscribe a connection from a stream.
    Task UnsubscribeAsync(string connectionId, string stream);

    /// Remove all subscriptions for a connection (call from OnDisconnectedAsync).
    Task RemoveConnectionAsync(string connectionId);
}
```

On each `SubscribeAsync` call, the gateway:

1. Creates a `RouteAndTransform` for this connection + stream (serializes events to envelopes)
2. Creates a `GatewayHandler<SignalRProduceOptions>` with the `SignalRProducer` + transform
3. Assembles the consume pipeline (handler → `DefaultConsumer` → `ConsumerFilter` → `ConsumePipe`)
4. Calls the `SubscriptionFactory` to create a store-specific subscription
5. Tracks in `ConcurrentDictionary<(string connectionId, string stream), SubscriptionState>`
6. Starts the subscription in a background task

On unsubscribe or disconnect, cancels and disposes.

**Store-agnostic subscription factory:**

```csharp
public delegate IMessageSubscription SubscriptionFactory(
    StreamName stream,
    ulong? fromPosition,
    ConsumePipe pipe,
    string subscriptionId
);
```

Registered once at startup. The factory captures common dependencies (client, logger factory) from DI. The gateway calls it each time a new per-connection subscription is needed.

#### Ready-Made Hub

Optional convenience hub for the simple case:

```csharp
public class SignalRSubscriptionHub : Hub {
    public Task SubscribeToStream(string stream, ulong? fromPosition);
    public Task UnsubscribeFromStream(string stream);

    public override Task OnDisconnectedAsync(Exception? exception);
}
```

Delegates entirely to `SubscriptionGateway<SignalRSubscriptionHub>`. Applications with custom hubs call `SubscriptionGateway<THub>` directly instead.

#### DI Registration

```csharp
// Register gateway with KurrentDB subscription factory
services.AddSignalRSubscriptionGateway<SignalRSubscriptionHub>((sp, options) => {
    var client = sp.GetRequiredService<KurrentDBClient>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    options.SubscriptionFactory = (stream, fromPosition, pipe, subscriptionId) =>
        new StreamSubscription(client, new StreamSubscriptionOptions {
            StreamName = stream,
            SubscriptionId = subscriptionId
        }, new NoOpCheckpointStore(fromPosition), pipe, loggerFactory);
});

// Optionally map the ready-made hub
app.MapHub<SignalRSubscriptionHub>("/subscriptions");
```

### Eventuous.SignalR.Client

Client-side subscription management with auto-reconnect and two consumption APIs.

**Dependencies:**
- `Microsoft.AspNetCore.SignalR.Client` — hub connection
- `Eventuous.Shared` — `TypeMap` / `ITypeMapper` for typed consumption
- `Eventuous.Serialization` — `IEventSerializer` for deserialization

Wire contracts are compiled from linked source files (`<Compile Include="../Eventuous.SignalR.Server/Contracts/*.cs" />`), not from a package reference.

Note: `Eventuous.Subscriptions` is NOT needed on the client. The typed consumption uses `TypeMap` (from `Eventuous.Shared`) and `IEventSerializer` (from `Eventuous.Serialization`) directly.

#### SignalRSubscriptionClient

Manages the hub connection and active subscriptions:

```csharp
public class SignalRSubscriptionClient : IAsyncDisposable {
    SignalRSubscriptionClient(HubConnection connection, SignalRSubscriptionClientOptions? options = null);

    /// Raw streaming — returns envelopes as they arrive.
    IAsyncEnumerable<StreamEventEnvelope> SubscribeAsync(
        string stream, ulong? fromPosition, CancellationToken ct
    );

    /// Typed consumption — deserializes via TypeMap, dispatches to On<T> handlers.
    TypedStreamSubscription SubscribeTyped(
        string stream, ulong? fromPosition
    );

    /// Unsubscribe from a stream.
    Task UnsubscribeAsync(string stream);
}
```

**`IAsyncEnumerable` mode:** Uses a `Channel<StreamEventEnvelope>` internally. The SignalR `StreamEvent` callback writes to the channel, `SubscribeAsync` reads from it. Clean cancellation via `CancellationToken`.

**Typed mode:**

```csharp
public class TypedStreamSubscription : IAsyncDisposable {
    /// Register a typed handler (Eventuous idiom).
    TypedStreamSubscription On<T>(Func<T, StreamMeta, ValueTask> handler) where T : class;

    /// Error callback.
    TypedStreamSubscription OnError(Action<StreamSubscriptionError> handler);

    /// Start receiving events. Must be called after all On<T> handlers are registered.
    /// Calling On<T> after StartAsync throws InvalidOperationException.
    Task StartAsync(CancellationToken ct = default);

    ValueTask DisposeAsync();
}

public record StreamMeta(string Stream, ulong Position, DateTime Timestamp);
```

```csharp
public class SignalRSubscriptionClientOptions {
    /// Serializer for deserializing event payloads in typed mode. Uses DefaultEventSerializer if null.
    public IEventSerializer? Serializer { get; set; }

    /// When true, the client creates an Activity for each received event, linked to the
    /// trace context from metadata ($traceId/$spanId). Default: false.
    /// Enable when the client has an OpenTelemetry collector configured and wants to
    /// participate in distributed traces. Disable for UI consumers that don't report telemetry.
    public bool EnableTracing { get; set; }
}
```

Deserializes `JsonPayload` using `IEventSerializer`, dispatches to the matching `On<T>` handler based on `EventType` resolved via `TypeMap`. Unrecognized event types are silently skipped (same behavior as `EventHandler.Ignored`).

When `EnableTracing` is false (default), metadata is still carried on the envelope and available via `StreamMeta` for inspection, but no `Activity` is created. When true, the client deserializes `JsonMetadata` → `Metadata` → `GetTracingMeta()` → `ToActivityContext()` and starts a consume `Activity` with that parent context before invoking the handler.

Disposing a `TypedStreamSubscription` without calling `StartAsync` is safe (no server-side subscription was created). `StartAsync` is what triggers the `SubscribeToStream` call to the server.

#### Auto-Reconnect

The client handles connection drops transparently:

1. **Position tracking:** The client maintains `ConcurrentDictionary<string, ulong> _lastPositions` — updated atomically each time an event is received. Thread-safe because the SignalR callback and reconnect handler run on different threads.

2. **Reconnect detection:** Hooks into `HubConnection.Reconnected` (SignalR's built-in reconnect). When the connection comes back:
   - Iterates all active subscriptions
   - Re-sends `SubscribeToStream(stream, lastPosition)` for each
   - The server creates fresh subscriptions from those positions (it's stateless)

3. **Deduplication:** The client may receive events it already processed (the server doesn't know what was delivered pre-disconnect). The client deduplicates by stream position — if `event.StreamPosition <= _lastPositions[stream]`, skip.

4. **Connection configuration:** `HubConnection` should be configured with `.WithAutomaticReconnect()` by the application. The client hooks into the reconnect lifecycle but doesn't own the connection policy.

5. **Closed detection:** If the connection closes permanently (`HubConnection.Closed`), the client completes all active `IAsyncEnumerable` channels and fires `OnError` on typed subscriptions.

```
Normal flow:
  Client ──SubscribeToStream("Session-abc", 42)──► Server
  Client ◄──StreamEvent(envelope, pos=43)────────── Server
  Client ◄──StreamEvent(envelope, pos=44)────────── Server
         [tracks lastPosition = 44]

Disconnect + Reconnect:
  [connection drops]
  [SignalR reconnects automatically]
  Client ──SubscribeToStream("Session-abc", 44)──► Server  [re-subscribe from last position]
  Client ◄──StreamEvent(envelope, pos=44)────────── Server  [duplicate, skip]
  Client ◄──StreamEvent(envelope, pos=45)────────── Server  [new, deliver]
```

## Server Design Details

### Subscription Lifecycle

```
SubscribeAsync(connId, "Session-abc", 42)
  │
  ├─ Create RouteAndTransform (serializes events → StreamEventEnvelope)
  ├─ Create GatewayHandler<SignalRProduceOptions>(signalRProducer, transform)
  ├─ Wrap in DefaultConsumer → ConsumerFilter → ConsumePipe
  ├─ Call SubscriptionFactory(stream, position, pipe, id)
  ├─ Store SubscriptionState { CTS, IMessageSubscription } in _subscriptions[(connId, "Session-abc")]
  └─ Start subscription.Subscribe(...) in background Task

GatewayHandler.HandleEvent(context):
  ├─ Calls RouteAndTransform → StreamEventEnvelope
  └─ Calls SignalRProducer.Produce → hubContext.Clients.Client(connId).SendAsync("StreamEvent", envelope)

UnsubscribeAsync(connId, "Session-abc"):
  ├─ Cancel CTS
  ├─ Await subscription.Unsubscribe(...)
  └─ Remove from _subscriptions

RemoveConnectionAsync(connId):
  └─ For each subscription with matching connId: UnsubscribeAsync
```

### Error Handling

- If a subscription fails (stream deleted, permission denied), the `ForwardingHandler` catches the exception and sends `StreamError` to the client via the hub, then cleans up the subscription.
- If `SendAsync` fails (client disconnected mid-send), the subscription catches `IOException` / `HubException` and self-terminates. The `RemoveConnectionAsync` call from `OnDisconnectedAsync` handles final cleanup.

### Future: Shared Subscriptions with Fan-Out

The current design uses one Eventuous subscription per `(connectionId, stream)`. For high fan-out scenarios (many clients watching the same stream), this could be optimized to one subscription per stream with a set of connected clients.

The `SubscriptionGateway` API doesn't change — `SubscribeAsync` / `UnsubscribeAsync` remain the same. Internally, the key changes from `(connId, stream)` to just `stream`, and `ForwardingHandler` broadcasts to all connected clients for that stream instead of one.

This is a future optimization. The per-connection model is correct for the initial release and handles the typical case (a handful of clients viewing different streams).

### Future: `$all` Subscriptions

The current `SubscriptionFactory` takes a `StreamName`, which works for individual streams. Subscribing to `$all` (all events across streams) would require a different subscription type (e.g., `AllStreamSubscription`). The current API has no way to express this distinction. This can be addressed in a future version by extending the factory signature or adding a separate `SubscribeToAllAsync` method.

## Testing Strategy

- **Unit tests:** `SubscriptionGateway` with mock `IHubContext` and a fake subscription factory. Verify subscribe/unsubscribe lifecycle, cleanup on disconnect, error forwarding.
- **Unit tests:** `SignalRSubscriptionClient` with a mock `HubConnection`. Verify reconnect re-subscription, deduplication by position, typed dispatch, edge cases (dispose without start, On<T> after start).
- **Integration tests:** End-to-end with KurrentDB testcontainer + real SignalR connection. Subscribe, write events, verify delivery. Simulate disconnect + reconnect, verify resume from correct position with no gaps or duplicates.

## Non-Goals

- **Backpressure / slow consumer handling** — out of scope for v1. If a client can't keep up, events queue in SignalR's internal buffer.
- **Stream-level authorization** — hub-level auth gates the connection. Per-stream authorization (rejecting subscriptions to specific streams) is left to the application. Consider adding an optional `Func<string connectionId, string stream, bool>` predicate to the gateway in a future version.
- **Persistent subscriptions** — the server is stateless. No checkpoint persistence. The client provides the resume position.
