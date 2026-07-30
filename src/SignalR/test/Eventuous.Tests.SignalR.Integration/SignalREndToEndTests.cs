extern alias SignalRClient;
using System.Runtime.InteropServices;
using Eventuous.KurrentDB;
using Eventuous.SignalR.Server;
using KurrentDB.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.KurrentDb;
using ClientTypes = SignalRClient::Eventuous.SignalR.Client;
using KurrentStreamSubscription = Eventuous.KurrentDB.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.SignalR.Integration;

[EventType("TestOrderPlaced")]
record TestOrderPlaced(string OrderId, decimal Amount);

[EventType("TestOrderShipped")]
record TestOrderShipped(string OrderId, string TrackingNumber);

public class SignalREndToEndTests : IAsyncDisposable {
    KurrentDbContainer _container  = null!;
    WebApplication     _app        = null!;
    TestServer         _server     = null!;
    IEventStore        _eventStore = null!;

    [Before(Test)]
    public async Task Setup() {
        TypeMap.Instance.RegisterKnownEventTypes(typeof(TestOrderPlaced).Assembly);

        var image = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "kurrentplatform/kurrentdb:25.1.3-experimental-arm64-8.0-jammy"
            : "kurrentplatform/kurrentdb:25.1.3";

        _container = new KurrentDbBuilder()
            .WithImage(image)
            .WithEnvironment("KURRENTDB_ENABLE_ATOM_PUB_OVER_HTTP", "true")
            .Build();

        await _container.StartAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        builder.Services.AddSignalR();
        builder.Services.AddKurrentDBClient(_container.GetConnectionString());
        builder.Services.AddEventStore<KurrentDBEventStore>();

        builder.Services.AddSignalRSubscriptionGateway<SignalRSubscriptionHub>((sp, options) => {
                var client        = sp.GetRequiredService<KurrentDBClient>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                options.SubscriptionFactory = (stream, fromPosition, pipe, subscriptionId) =>
                    new KurrentStreamSubscription(
                        client,
                        new() { StreamName = stream, SubscriptionId = subscriptionId },
                        new NoOpCheckpointStore(fromPosition),
                        pipe,
                        loggerFactory
                    );
            }
        );

        _app = builder.Build();
        _app.MapHub<SignalRSubscriptionHub>("/subscriptions");

        await _app.StartAsync();

        _server     = _app.GetTestServer();
        _eventStore = _app.Services.GetRequiredService<IEventStore>();
    }

    HubConnection CreateHubConnection() =>
        new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/subscriptions",
                opts => opts.HttpMessageHandlerFactory = _ => _server.CreateHandler()
            )
            .Build();

    async Task AppendEvents(string stream, params object[] events) {
        var streamEvents = events
            .Select(e => new NewStreamEvent(Guid.NewGuid(), e, new()))
            .ToArray();

        await _eventStore.AppendEvents(new(stream), ExpectedStreamVersion.Any, streamEvents, default);
    }

    [Test]
    public async Task RawStreaming_ReceivesAppendedEvents() {
        var stream = $"Order-{Guid.NewGuid():N}";

        await AppendEvents(stream, new TestOrderPlaced("order-1", 99.99m), new TestOrderShipped("order-1", "TRACK-123"));

        var connection = CreateHubConnection();
        await connection.StartAsync();
        var client = new ClientTypes.SignalRSubscriptionClient(connection);

        var received = new List<SignalRClient::Eventuous.SignalR.StreamEventEnvelope>();
        var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await foreach (var envelope in client.SubscribeAsync(stream, null, cts.Token)) {
            received.Add(envelope);

            if (received.Count >= 2) break;
        }

        await Assert.That(received).HasCount().EqualTo(2);
        await Assert.That(received[0].EventType).IsEqualTo("TestOrderPlaced");
        await Assert.That(received[1].EventType).IsEqualTo("TestOrderShipped");
        await Assert.That(received[0].StreamPosition).IsEqualTo(0UL);
        await Assert.That(received[1].StreamPosition).IsEqualTo(1UL);
        await Assert.That(received[0].JsonPayload).Contains("order-1");

        await client.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Test]
    public async Task TypedSubscription_DispatchesToCorrectHandlers() {
        var stream = $"Order-{Guid.NewGuid():N}";

        await AppendEvents(stream, new TestOrderPlaced("order-2", 149.99m), new TestOrderShipped("order-2", "TRACK-456"));

        var connection = CreateHubConnection();
        await connection.StartAsync();
        var client = new ClientTypes.SignalRSubscriptionClient(connection);

        var placedEvents  = new List<TestOrderPlaced>();
        var shippedEvents = new List<TestOrderShipped>();
        var done          = new TaskCompletionSource();
        var count         = 0;

        var sub = client.SubscribeTyped(stream, null)
            .On<TestOrderPlaced>((evt, _) => {
                    placedEvents.Add(evt);
                    if (Interlocked.Increment(ref count) >= 2) done.TrySetResult();

                    return ValueTask.CompletedTask;
                }
            )
            .On<TestOrderShipped>((evt, _) => {
                    shippedEvents.Add(evt);
                    if (Interlocked.Increment(ref count) >= 2) done.TrySetResult();

                    return ValueTask.CompletedTask;
                }
            );

        await sub.StartAsync();

        var completed = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        await Assert.That(completed).IsEqualTo(done.Task);

        await Assert.That(placedEvents).HasCount().EqualTo(1);
        await Assert.That(placedEvents[0].OrderId).IsEqualTo("order-2");
        await Assert.That(placedEvents[0].Amount).IsEqualTo(149.99m);

        await Assert.That(shippedEvents).HasCount().EqualTo(1);
        await Assert.That(shippedEvents[0].TrackingNumber).IsEqualTo("TRACK-456");

        await sub.DisposeAsync();
        await client.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Test]
    public async Task LiveEvents_DeliveredAfterSubscribe() {
        var stream = $"Order-{Guid.NewGuid():N}";

        var connection = CreateHubConnection();
        await connection.StartAsync();
        var client = new ClientTypes.SignalRSubscriptionClient(connection);

        var received = new List<SignalRClient::Eventuous.SignalR.StreamEventEnvelope>();
        var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var consumeTask = Task.Run(
            async () => {
                await foreach (var envelope in client.SubscribeAsync(stream, null, cts.Token)) {
                    received.Add(envelope);

                    if (received.Count >= 2) break;
                }
            },
            cts.Token
        );

        // Give the subscription time to start on the server
        await Task.Delay(1000, cts.Token);

        await AppendEvents(stream, new TestOrderPlaced("order-3", 200m), new TestOrderShipped("order-3", "TRACK-789"));

        await consumeTask.WaitAsync(TimeSpan.FromSeconds(10), cts.Token);

        await Assert.That(received).HasCount().EqualTo(2);
        await Assert.That(received[0].EventType).IsEqualTo("TestOrderPlaced");
        await Assert.That(received[1].EventType).IsEqualTo("TestOrderShipped");

        await client.DisposeAsync();
        await connection.DisposeAsync();
    }

    public async ValueTask DisposeAsync() {
        await _app.DisposeAsync();
        await _container.DisposeAsync();
    }
}
