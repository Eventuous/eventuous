# SignalR Subscription Gateway Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two NuGet packages (`Eventuous.SignalR.Server`, `Eventuous.SignalR.Client`) that relay Eventuous stream subscriptions over SignalR with auto-reconnect and typed event handling.

**Spec:** `docs/superpowers/specs/2026-03-19-signalr-subscription-gateway-design.md`

**Architecture:** Server reuses the existing Gateway pattern (`GatewayHandler` + `BaseProducer`) to forward events over SignalR. Client provides `IAsyncEnumerable` and typed `On<T>` consumption with position-based auto-reconnect. Wire contracts are source-shared between projects (no shared binary package).

**Tech Stack:** .NET 10/9/8, ASP.NET Core SignalR, Eventuous.Subscriptions, Eventuous.Gateway, Eventuous.Producers, TUnit

---

## File Structure

### Server package: `src/SignalR/src/Eventuous.SignalR.Server/`

| File | Responsibility |
|------|---------------|
| `Contracts/StreamEventEnvelope.cs` | Wire DTO for events (source-linked to client) |
| `Contracts/StreamSubscriptionError.cs` | Wire DTO for errors (source-linked to client) |
| `Contracts/SignalRSubscriptionMethods.cs` | Hub method name constants (source-linked to client) |
| `SignalRProduceOptions.cs` | Produce options record (connectionId) |
| `SignalRProducer.cs` | `BaseProducer<SignalRProduceOptions>` — sends envelopes via `IHubContext` |
| `SignalRTransform.cs` | `RouteAndTransform` factory — serializes `IMessageConsumeContext` to `StreamEventEnvelope` |
| `SubscriptionGateway.cs` | Per-connection subscription lifecycle manager |
| `SignalRSubscriptionHub.cs` | Ready-made convenience hub |
| `SignalRGatewayOptions.cs` | Options class with `SubscriptionFactory` |
| `Registrations/SignalRGatewayRegistrations.cs` | DI extension methods |
| `Eventuous.SignalR.Server.csproj` | Project file |

### Client package: `src/SignalR/src/Eventuous.SignalR.Client/`

| File | Responsibility |
|------|---------------|
| `SignalRSubscriptionClient.cs` | Hub connection manager, subscription lifecycle, auto-reconnect |
| `SignalRSubscriptionClientOptions.cs` | Client options (serializer, tracing toggle) |
| `TypedStreamSubscription.cs` | `On<T>` handler registration, deserialization, dispatch |
| `StreamMeta.cs` | Metadata record passed to typed handlers |
| `Eventuous.SignalR.Client.csproj` | Project file (links contract .cs files from Server) |

### Tests: `src/SignalR/test/Eventuous.Tests.SignalR/`

| File | Responsibility |
|------|---------------|
| `SignalRProducerTests.cs` | Producer sends envelopes via mock hub clients |
| `SignalRTransformTests.cs` | Transform serializes events correctly |
| `SubscriptionGatewayTests.cs` | Gateway lifecycle: subscribe, unsubscribe, disconnect cleanup |
| `SignalRSubscriptionClientTests.cs` | Client reconnect, dedup, IAsyncEnumerable, error handling |
| `TypedStreamSubscriptionTests.cs` | On<T> dispatch, unknown types ignored, StartAsync guards |
| `Eventuous.Tests.SignalR.csproj` | Test project file |

---

### Task 0: Scaffold project files and solution entries

**Files:**
- Create: `src/SignalR/src/Eventuous.SignalR.Server/Eventuous.SignalR.Server.csproj`
- Create: `src/SignalR/src/Eventuous.SignalR.Client/Eventuous.SignalR.Client.csproj`
- Create: `src/SignalR/test/Eventuous.Tests.SignalR/Eventuous.Tests.SignalR.csproj`
- Modify: `Eventuous.slnx`

- [ ] **Step 1:** Create server `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <ItemGroup>
        <ProjectReference Include="$(CoreRoot)\Eventuous.Producers\Eventuous.Producers.csproj" />
        <ProjectReference Include="$(CoreRoot)\Eventuous.Subscriptions\Eventuous.Subscriptions.csproj" />
        <ProjectReference Include="$(SrcRoot)\Gateway\src\Eventuous.Gateway\Eventuous.Gateway.csproj" />
    </ItemGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.AspNetCore.SignalR.Common" />
    </ItemGroup>
    <ItemGroup>
        <Using Include="Eventuous.Producers" />
        <Using Include="Eventuous.Subscriptions" />
        <Using Include="Eventuous.Gateway" />
    </ItemGroup>
    <ItemGroup>
        <Compile Include="$(CoreRoot)\Eventuous.Shared\Tools\TaskExtensions.cs">
            <Link>Tools\TaskExtensions.cs</Link>
        </Compile>
        <Using Include="Eventuous.Tools" />
    </ItemGroup>
</Project>
```

- [ ] **Step 2:** Create client `.csproj` with source-linked contracts:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <ItemGroup>
        <ProjectReference Include="$(CoreRoot)\Eventuous.Shared\Eventuous.Shared.csproj" />
        <ProjectReference Include="$(CoreRoot)\Eventuous.Serialization\Eventuous.Serialization.csproj" />
    </ItemGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />
    </ItemGroup>
    <ItemGroup>
        <Compile Include="..\Eventuous.SignalR.Server\Contracts\*.cs" LinkBase="Contracts" />
    </ItemGroup>
</Project>
```

- [ ] **Step 3:** Create test `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include="$(LocalRoot)\Eventuous.SignalR.Server\Eventuous.SignalR.Server.csproj" />
        <ProjectReference Include="$(LocalRoot)\Eventuous.SignalR.Client\Eventuous.SignalR.Client.csproj" />
    </ItemGroup>
    <ItemGroup>
        <PackageReference Include="NSubstitute" />
    </ItemGroup>
</Project>
```

- [ ] **Step 4:** Add solution folder entries to `Eventuous.slnx`:

```xml
<Folder Name="/SignalR/" />
<Folder Name="/SignalR/src/">
    <Project Path="src/SignalR/src/Eventuous.SignalR.Server/Eventuous.SignalR.Server.csproj" />
    <Project Path="src/SignalR/src/Eventuous.SignalR.Client/Eventuous.SignalR.Client.csproj" />
</Folder>
<Folder Name="/SignalR/test/">
    <Project Path="src/SignalR/test/Eventuous.Tests.SignalR/Eventuous.Tests.SignalR.csproj" />
</Folder>
```

- [ ] **Step 5:** Build solution to verify scaffold: `dotnet build Eventuous.slnx`
- [ ] **Step 6: Commit**

```bash
git add src/SignalR/ Eventuous.slnx
git commit -m "feat(signalr): scaffold Server, Client, and test projects"
```

---

### Task 1: Wire contracts

**Files:**
- Create: `src/SignalR/src/Eventuous.SignalR.Server/Contracts/StreamEventEnvelope.cs`
- Create: `src/SignalR/src/Eventuous.SignalR.Server/Contracts/StreamSubscriptionError.cs`
- Create: `src/SignalR/src/Eventuous.SignalR.Server/Contracts/SignalRSubscriptionMethods.cs`

- [ ] **Step 1:** Create `StreamEventEnvelope.cs`:

```csharp
namespace Eventuous.SignalR;

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

- [ ] **Step 2:** Create `StreamSubscriptionError.cs`:

```csharp
namespace Eventuous.SignalR;

public record StreamSubscriptionError {
    public required string Stream { get; init; }
    public required string Message { get; init; }
}
```

- [ ] **Step 3:** Create `SignalRSubscriptionMethods.cs`:

```csharp
namespace Eventuous.SignalR;

public static class SignalRSubscriptionMethods {
    public const string Subscribe = "SubscribeToStream";
    public const string Unsubscribe = "UnsubscribeFromStream";
    public const string StreamEvent = "StreamEvent";
    public const string StreamError = "StreamError";
}
```

- [ ] **Step 4:** Build to verify contracts compile in both Server and Client (via source link): `dotnet build Eventuous.slnx`
- [ ] **Step 5: Commit**

```bash
git add src/SignalR/src/Eventuous.SignalR.Server/Contracts/
git commit -m "feat(signalr): add wire contracts (StreamEventEnvelope, error, method names)"
```

---

### Task 2: SignalRProducer

**Files:**
- Create: `src/SignalR/src/Eventuous.SignalR.Server/SignalRProduceOptions.cs`
- Create: `src/SignalR/src/Eventuous.SignalR.Server/SignalRProducer.cs`
- Create: `src/SignalR/test/Eventuous.Tests.SignalR/SignalRProducerTests.cs`

- [ ] **Step 1:** Create `SignalRProduceOptions.cs`:

```csharp
namespace Eventuous.SignalR.Server;

public record SignalRProduceOptions(string ConnectionId);
```

- [ ] **Step 2:** Write failing test for producer — verify it sends the envelope to the correct connection:

```csharp
using Eventuous.SignalR;
using Eventuous.SignalR.Server;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace Eventuous.Tests.SignalR;

public class SignalRProducerTests {
    [Test]
    public async Task ProduceMessages_SendsEnvelopeToCorrectConnection() {
        var hubContext = Substitute.For<IHubContext<TestHub>>();
        var clientProxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Client("conn-1").Returns(clientProxy);

        var producer = new SignalRProducer<TestHub>(hubContext);
        var envelope = new StreamEventEnvelope {
            EventId = Guid.NewGuid(),
            Stream = "Test-1",
            EventType = "TestEvent",
            StreamPosition = 0,
            GlobalPosition = 0,
            Timestamp = DateTime.UtcNow,
            JsonPayload = "{}"
        };

        await producer.Produce(
            new StreamName("Test-1"),
            [new ProducedMessage(envelope, new Metadata())],
            new SignalRProduceOptions("conn-1")
        );

        await clientProxy.Received(1).SendCoreAsync(
            SignalRSubscriptionMethods.StreamEvent,
            Arg.Is<object?[]>(args => args.Length == 1 && args[0] is StreamEventEnvelope),
            Arg.Any<CancellationToken>()
        );
    }
}

public class TestHub : Hub;
```

- [ ] **Step 3:** Run test to verify it fails: `dotnet test src/SignalR/test/Eventuous.Tests.SignalR/ -f net10.0`
- [ ] **Step 4:** Create `SignalRProducer.cs`:

```csharp
using System.Text;
using Microsoft.AspNetCore.SignalR;

namespace Eventuous.SignalR.Server;

public class SignalRProducer<THub>(IHubContext<THub> hubContext)
    : BaseProducer<SignalRProduceOptions>(new ProducerTracingOptions { ProducerName = "signalr" })
    where THub : Hub {

    protected override async Task ProduceMessages(
        StreamName stream,
        IEnumerable<ProducedMessage> messages,
        SignalRProduceOptions? options,
        CancellationToken cancellationToken = default
    ) {
        var client = hubContext.Clients.Client(options!.ConnectionId);

        foreach (var msg in messages) {
            await client.SendAsync(
                SignalRSubscriptionMethods.StreamEvent,
                msg.Message,
                cancellationToken
            );
        }
    }
}
```

- [ ] **Step 5:** Run test to verify it passes
- [ ] **Step 6: Commit**

```bash
git add src/SignalR/
git commit -m "feat(signalr): add SignalRProducer extending BaseProducer"
```

---

### Task 3: SignalRTransform

**Files:**
- Create: `src/SignalR/src/Eventuous.SignalR.Server/SignalRTransform.cs`
- Create: `src/SignalR/test/Eventuous.Tests.SignalR/SignalRTransformTests.cs`

- [ ] **Step 1:** Write failing test — transform creates correct envelope from consume context:

```csharp
using System.Text.Json;
using Eventuous.SignalR;
using Eventuous.SignalR.Server;
using Eventuous.Subscriptions.Context;
using NSubstitute;

namespace Eventuous.Tests.SignalR;

public class SignalRTransformTests {
    [Test]
    public async Task Transform_CreatesCorrectEnvelope() {
        var serializer = DefaultEventSerializer.Instance;
        var transform = SignalRTransform.Create("conn-1", "Test-1", serializer);

        var ctx = CreateConsumeContext(
            eventId: "aabbccdd-1234-5678-9012-aabbccddeeff",
            eventType: "TestEvent",
            stream: "Test-1",
            streamPosition: 5,
            globalPosition: 42,
            message: new TestEvent("hello")
        );

        var result = await transform(ctx);

        await Assert.That(result).HasCount().EqualTo(1);

        var envelope = (StreamEventEnvelope)result[0].Message;
        await Assert.That(envelope.Stream).IsEqualTo("Test-1");
        await Assert.That(envelope.EventType).IsEqualTo("TestEvent");
        await Assert.That(envelope.StreamPosition).IsEqualTo(5UL);
        await Assert.That(envelope.GlobalPosition).IsEqualTo(42UL);
        await Assert.That(envelope.JsonPayload).IsNotNull();

        var options = result[0].ProduceOptions;
        await Assert.That(options.ConnectionId).IsEqualTo("conn-1");
    }

    static IMessageConsumeContext CreateConsumeContext(
        string eventId, string eventType, string stream,
        ulong streamPosition, ulong globalPosition, object message
    ) => new MessageConsumeContext(
        eventId, eventType, "application/json", stream,
        0, streamPosition, globalPosition, 0,
        DateTime.UtcNow, message, null, "test-sub", CancellationToken.None
    );
}

[EventType("TestEvent")]
record TestEvent(string Value);
```

- [ ] **Step 2:** Run test to verify it fails
- [ ] **Step 3:** Create `SignalRTransform.cs`:

```csharp
using System.Text;
using System.Text.Json;
using Eventuous.Subscriptions.Context;

namespace Eventuous.SignalR.Server;

public static class SignalRTransform {
    public static RouteAndTransform<SignalRProduceOptions> Create(
        string connectionId, string stream, IEventSerializer serializer
    ) => ctx => {
        var result = serializer.SerializeEvent(ctx.Message!);
        var envelope = new StreamEventEnvelope {
            EventId        = Guid.TryParse(ctx.MessageId, out var id) ? id : Guid.NewGuid(),
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
}
```

- [ ] **Step 4:** Run test to verify it passes
- [ ] **Step 5: Commit**

```bash
git add src/SignalR/
git commit -m "feat(signalr): add SignalRTransform (RouteAndTransform factory)"
```

---

### Task 4: SubscriptionGateway

**Files:**
- Create: `src/SignalR/src/Eventuous.SignalR.Server/SignalRGatewayOptions.cs`
- Create: `src/SignalR/src/Eventuous.SignalR.Server/SubscriptionGateway.cs`
- Create: `src/SignalR/test/Eventuous.Tests.SignalR/SubscriptionGatewayTests.cs`

- [ ] **Step 1:** Create `SignalRGatewayOptions.cs`:

```csharp
namespace Eventuous.SignalR.Server;

public delegate IMessageSubscription SubscriptionFactory(
    StreamName stream, ulong? fromPosition, ConsumePipe pipe, string subscriptionId
);

public class SignalRGatewayOptions {
    public required SubscriptionFactory SubscriptionFactory { get; set; }
}
```

- [ ] **Step 2:** Write failing tests for gateway lifecycle:

```csharp
// Tests:
// 1. SubscribeAsync creates a subscription via factory
// 2. UnsubscribeAsync cancels and removes subscription
// 3. RemoveConnectionAsync cleans up all subscriptions for a connection
// 4. DisposeAsync cleans up everything
// 5. Duplicate subscribe to same (connId, stream) replaces previous
```

(Test file will use mock `IHubContext<TestHub>`, mock `IEventSerializer`, and a fake `SubscriptionFactory` that records calls.)

- [ ] **Step 3:** Run tests to verify they fail
- [ ] **Step 4:** Implement `SubscriptionGateway.cs`:

The gateway creates `GatewayHandler<SignalRProduceOptions>` using `SignalRTransform.Create` and `SignalRProducer`, assembles the `ConsumePipe` with `DefaultConsumer` → `ConsumerFilter`, calls the factory, and manages subscription state in a `ConcurrentDictionary`.

Key implementation detail: `GatewayHandler` is internal — use `GatewayHandlerFactory.Create` (public API) which wraps the producer in a `GatewayProducer` internally. Use `pipe.AddDefaultConsumer(handler)` which handles `DefaultConsumer` → `ConsumerFilter` wrapping:

```csharp
var transform = SignalRTransform.Create(connectionId, stream, _eventSerializer);
var handler = GatewayHandlerFactory.Create(_producer, transform, awaitProduce: true);
var pipe = new ConsumePipe();
pipe.AddDefaultConsumer(handler);
```

- [ ] **Step 5:** Run tests to verify they pass
- [ ] **Step 6: Commit**

```bash
git add src/SignalR/
git commit -m "feat(signalr): add SubscriptionGateway with per-connection lifecycle"
```

---

### Task 5: Ready-made hub and DI registration

**Files:**
- Create: `src/SignalR/src/Eventuous.SignalR.Server/SignalRSubscriptionHub.cs`
- Create: `src/SignalR/src/Eventuous.SignalR.Server/Registrations/SignalRGatewayRegistrations.cs`

- [ ] **Step 1:** Create `SignalRSubscriptionHub.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;

namespace Eventuous.SignalR.Server;

public class SignalRSubscriptionHub(SubscriptionGateway<SignalRSubscriptionHub> gateway) : Hub {
    public Task SubscribeToStream(string stream, ulong? fromPosition)
        => gateway.SubscribeAsync(Context.ConnectionId, stream, fromPosition, Context.ConnectionAborted);

    public Task UnsubscribeFromStream(string stream)
        => gateway.UnsubscribeAsync(Context.ConnectionId, stream);

    public override Task OnDisconnectedAsync(Exception? exception)
        => gateway.RemoveConnectionAsync(Context.ConnectionId);
}
```

- [ ] **Step 2:** Create DI registration in `Registrations/SignalRGatewayRegistrations.cs`:

```csharp
using Eventuous.SignalR.Server;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Extensions.DependencyInjection;

public static class SignalRGatewayRegistrations {
    public static IServiceCollection AddSignalRSubscriptionGateway<THub>(
        this IServiceCollection services,
        Action<IServiceProvider, SignalRGatewayOptions> configure
    ) where THub : Hub {
        services.AddSingleton(sp => {
            var options = new SignalRGatewayOptions { SubscriptionFactory = null! };
            configure(sp, options);
            return options;
        });
        services.AddSingleton<SignalRProducer<THub>>();
        services.AddSingleton<SubscriptionGateway<THub>>();
        return services;
    }
}
```

- [ ] **Step 3:** Build: `dotnet build Eventuous.slnx`
- [ ] **Step 4: Commit**

```bash
git add src/SignalR/src/Eventuous.SignalR.Server/
git commit -m "feat(signalr): add SignalRSubscriptionHub and DI registrations"
```

---

### Task 6: Client — SignalRSubscriptionClient with IAsyncEnumerable

**Files:**
- Create: `src/SignalR/src/Eventuous.SignalR.Client/SignalRSubscriptionClientOptions.cs`
- Create: `src/SignalR/src/Eventuous.SignalR.Client/SignalRSubscriptionClient.cs`
- Create: `src/SignalR/test/Eventuous.Tests.SignalR/SignalRSubscriptionClientTests.cs`

- [ ] **Step 1:** Create `SignalRSubscriptionClientOptions.cs`:

```csharp
namespace Eventuous.SignalR.Client;

public class SignalRSubscriptionClientOptions {
    public IEventSerializer? Serializer { get; set; }
    public bool EnableTracing { get; set; }
}
```

- [ ] **Step 2:** Write failing tests for client:

```csharp
// Tests:
// 1. SubscribeAsync returns IAsyncEnumerable that yields envelopes from hub callback
// 2. Deduplication: events with position <= last seen are skipped
// 3. Reconnect: re-subscribes all active streams with last known position
// 4. Closed: completes channels when connection closes permanently
// 5. UnsubscribeAsync stops receiving events for that stream
// 6. DisposeAsync cleans up all subscriptions
```

- [ ] **Step 3:** Run tests to verify they fail
- [ ] **Step 4:** Implement `SignalRSubscriptionClient.cs`:

Core design:
- `ConcurrentDictionary<string, SubscriptionState>` tracks active subscriptions (stream → channel + lastPosition)
- Constructor hooks `HubConnection.Reconnected` and `HubConnection.Closed`
- `SubscribeAsync` creates a `Channel<StreamEventEnvelope>`, registers the `StreamEvent` hub callback (if not already), calls hub `SubscribeToStream`, returns `channel.Reader.ReadAllAsync()`
- Hub callback routes envelopes to the correct channel by stream name, skips if position ≤ last seen
- Reconnect handler iterates all active subscriptions and re-sends `SubscribeToStream` with last position

- [ ] **Step 5:** Run tests to verify they pass
- [ ] **Step 6: Commit**

```bash
git add src/SignalR/
git commit -m "feat(signalr): add SignalRSubscriptionClient with IAsyncEnumerable and auto-reconnect"
```

---

### Task 7: Client — TypedStreamSubscription with On<T>

**Files:**
- Create: `src/SignalR/src/Eventuous.SignalR.Client/StreamMeta.cs`
- Create: `src/SignalR/src/Eventuous.SignalR.Client/TypedStreamSubscription.cs`
- Create: `src/SignalR/test/Eventuous.Tests.SignalR/TypedStreamSubscriptionTests.cs`

- [ ] **Step 1:** Create `StreamMeta.cs`:

```csharp
namespace Eventuous.SignalR.Client;

public record StreamMeta(string Stream, ulong Position, DateTime Timestamp);
```

- [ ] **Step 2:** Write failing tests:

```csharp
// Tests:
// 1. On<T> handler receives deserialized event with correct StreamMeta
// 2. Unknown event types are silently skipped
// 3. Calling On<T> after StartAsync throws InvalidOperationException
// 4. Dispose without StartAsync is safe (no-op)
// 5. OnError callback fires on StreamSubscriptionError
// 6. When EnableTracing=true, Activity is created with parent from metadata
// 7. When EnableTracing=false (default), no Activity is created
```

- [ ] **Step 3:** Run tests to verify they fail
- [ ] **Step 4:** Implement `TypedStreamSubscription.cs`:

Core design:
- Constructor takes `SignalRSubscriptionClient`, stream, position, options
- `On<T>` registers handler in `Dictionary<Type, Delegate>`, guarded by `_started` flag
- `StartAsync` calls `client.SubscribeAsync` internally and starts a background loop reading the `IAsyncEnumerable`
- For each envelope: deserialize `JsonPayload` via `IEventSerializer`, resolve type via `TypeMap`, dispatch to matching handler
- If `EnableTracing`: deserialize `JsonMetadata` → `Metadata` → `GetTracingMeta()` → `ToActivityContext()` → start `Activity`
- Dispose cancels the background loop

- [ ] **Step 5:** Run tests to verify they pass
- [ ] **Step 6: Commit**

```bash
git add src/SignalR/
git commit -m "feat(signalr): add TypedStreamSubscription with On<T> and optional tracing"
```

---

### Task 8: Final verification and cleanup

**Files:**
- Possibly modify: any files needing cleanup from review

- [ ] **Step 1:** Build entire solution: `dotnet build Eventuous.slnx`
- [ ] **Step 2:** Run all SignalR tests: `dotnet test src/SignalR/test/Eventuous.Tests.SignalR/ -f net10.0`
- [ ] **Step 3:** Run full solution tests to check for regressions: `dotnet test Eventuous.slnx -f net10.0`
- [ ] **Step 4:** Review public API surface — ensure all public types have XML doc comments
- [ ] **Step 5: Commit any cleanup**

```bash
git add -A
git commit -m "chore(signalr): final cleanup and XML docs"
```

---

## Notes

- **TUnit test framework:** All tests use `[Test]` attribute and `await Assert.That(...)` assertions. Test projects need `<OutputType>Exe</OutputType>`.
- **Multi-targeting:** Projects target `net10.0;net9.0;net8.0` (inherited from `Directory.Build.props`). Tests run on `net10.0` by default.
- **NSubstitute** is used for mocking `IHubContext`, `IClientProxy`, etc. Check if it's already in `Directory.Packages.props`; if not, add it.
- **No integration tests in this plan.** Integration tests with KurrentDB testcontainer + real SignalR can be added as a follow-up. The unit tests verify all behavioral contracts.
- **Namespace convention:** Contracts use `Eventuous.SignalR` (shared namespace). Server types use `Eventuous.SignalR.Server`. Client types use `Eventuous.SignalR.Client`.
