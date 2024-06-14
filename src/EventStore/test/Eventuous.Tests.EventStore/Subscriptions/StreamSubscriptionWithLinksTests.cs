using System.Collections.Concurrent;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Context;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.DependencyInjection;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class StreamSubscriptionWithLinksTests : StoreFixture {
    const string SubId = "Test";

    readonly List<Checkpoint>  _checkpoints = [];
    readonly string            _prefix      = $"{Faker.Commerce.ProductAdjective()}{Faker.Commerce.Product()}";

    public StreamSubscriptionWithLinksTests(ITestOutputHelper output) {
        Output   = output;
        AutoStart = false;
    }

    [Fact]
    [Trait("Category", "Special cases")]
    public async Task ShouldHandleAllEventsFromStart() {
        await Start();
        await Execute(1000, null);
    }

    [Fact]
    [Trait("Category", "Special cases")]
    public async Task ShouldHandleHalfOfTheEvents() {
        const int count         = 1000;
        const int expectedCount = count / 2;

        var checkpointStore = Provider.GetRequiredService<NoOpCheckpointStore>();
        await checkpointStore.StoreCheckpoint(new Checkpoint(SubId, expectedCount - 1), true, default);

        await Start();
        await Execute(count, expectedCount);
    }

    async Task Execute(int count, ulong? expectedCount) {
        var events = await Seed(Provider, count);
        await WaitForCheckpoint(count, 10.Seconds());
        ValidateProcessed(Provider, expectedCount == null ? events : events.Skip((int)expectedCount.Value));
        ValidateCheckpoint(count);
    }

    async Task<List<TestEvent>> Seed(IServiceProvider provider, int count) {
        TypeMap.Instance.AddType<TestEvent>(TestEvent.TypeName);
        var producer = provider.GetRequiredService<IEventProducer>();

        Output?.WriteLine("Producing events...");

        var events = new List<TestEvent>();
        for (var i = 0; i < count; i++) {
            var evt    = new TestEvent(Guid.NewGuid().ToString(), i);
            var stream = new StreamName($"{_prefix}-{Auto.Create<string>()}");
            await producer.Produce(stream, evt, null);
            events.Add(evt);
        }

        Output?.WriteLine("Producing complete");

        return events;
    }

    void ValidateProcessed(IServiceProvider provider, IEnumerable<TestEvent> events) {
        var handler = provider.GetRequiredKeyedService<TestHandler>(SubId);
        Output?.WriteLine($"Processed {handler.Handled.Count} events");
        // handler.Handled.Should().BeEquivalentTo(events);
        foreach (var evt in events) {
            handler.Handled.Should().Contain(evt);
        }
    }

    void ValidateCheckpoint(int count) {
        _checkpoints.Count.Should().BeGreaterThan(0);
        _checkpoints.Skip(1).Select(x => x.Position).Should().NotContain(0);
        _checkpoints.Last().Position.Should().Be((ulong)(count - 1));
    }

    async Task WaitForCheckpoint(int count, TimeSpan deadline) {
        using var source = new CancellationTokenSource(deadline);

        var expected = (ulong)(count - 1);

        try {
            while (!source.IsCancellationRequested) {
                var last = _checkpoints.LastOrDefault().Position;

                if (last >= expected) {
                    return;
                }

                await Task.Delay(500, source.Token);
            }
        } catch (OperationCanceledException) {
            Output?.WriteLine("Deadline exceeded");
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    class TestHandler(ILogger<TestHandler> logger) : BaseEventHandler {
        public ConcurrentBag<object> Handled { get; } = [];

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext ctx) {
            Handled.Add(ctx.Message!);

            logger.LogDebug("Handled event from {Stream} at {Position}", ctx.Stream, ctx.StreamPosition);

            return ValueTask.FromResult(EventHandlingStatus.Success);
        }
    }

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);
        services.AddProducer<EventStoreProducer>();

        services
            .AddSubscription<StreamSubscription, StreamSubscriptionOptions>(
                SubId,
                builder => builder
                    .Configure(
                        x => {
                            x.StreamName       = new StreamName($"$ce-{_prefix}");
                            x.ConcurrencyLimit = 5;
                            x.ResolveLinkTos   = true;
                        }
                    )
                    .AddEventHandler<TestHandler>()
            );
        var checkpointStore = new NoOpCheckpointStore();
        checkpointStore.CheckpointStored += CheckpointStoreOnCheckpointStored;
        services.AddSingleton(checkpointStore);
        services.AddSingleton<ICheckpointStore>(sp => sp.GetRequiredService<NoOpCheckpointStore>());

        return;

        void CheckpointStoreOnCheckpointStored(object? sender, Checkpoint e) {
            Output?.WriteLine($"Stored checkpoint {e.Id}: {e.Position}");
            _checkpoints.Add(e);
        }
    }
}
