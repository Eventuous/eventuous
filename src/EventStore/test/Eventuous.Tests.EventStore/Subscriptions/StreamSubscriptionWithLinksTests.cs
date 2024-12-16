using System.Collections.Concurrent;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Context;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.DependencyInjection;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;
// ReSharper disable MethodHasAsyncOverload

namespace Eventuous.Tests.EventStore.Subscriptions;

public class StreamSubscriptionWithLinksTests : StoreFixture {
    const string SubId = "Test";

    readonly List<Checkpoint> _checkpoints = [];
    readonly string           _prefix      = $"{Faker.Commerce.ProductAdjective()}{Faker.Commerce.Product()}";

    public StreamSubscriptionWithLinksTests() {
        AutoStart = false;
        TypeMapper.AddType<TestEvent>(TestEvent.TypeName);
    }

    [Test]
    [Category("Special cases")]
    public async Task ShouldHandleAllEventsFromStart() {
        await Start();
        await Execute(1000, null);
    }

    [Test]
    [Category("Special cases")]
    public async Task ShouldHandleHalfOfTheEvents(CancellationToken cancellationToken) {
        const int count         = 1000;
        const int expectedCount = count / 2;

        var checkpointStore = Provider.GetRequiredService<NoOpCheckpointStore>();
        await checkpointStore.StoreCheckpoint(new(SubId, expectedCount - 1), true, cancellationToken);

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
        var producer = provider.GetRequiredService<IProducer>();

        TestContext.Current?.OutputWriter.WriteLine("Producing events...");

        var events = new List<TestEvent>();

        for (var i = 0; i < count; i++) {
            var evt    = new TestEvent(Guid.NewGuid().ToString(), i);
            var stream = new StreamName($"{_prefix}-{Guid.NewGuid():N}");
            await producer.Produce(stream, evt, null);
            events.Add(evt);
        }

        TestContext.Current?.OutputWriter.WriteLine("Producing complete");

        return events;
    }

    void ValidateProcessed(IServiceProvider provider, IEnumerable<TestEvent> events) {
        var handler = provider.GetRequiredKeyedService<TestHandler>(SubId);
        TestContext.Current?.OutputWriter.WriteLine($"Processed {handler.Handled.Count} events");

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
            TestContext.Current?.OutputWriter.WriteLine("Deadline exceeded");
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
                            x.StreamName       = new($"$ce-{_prefix}");
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
            TestContext.Current?.OutputWriter.WriteLine($"Stored checkpoint {e.Id}: {e.Position}");
            _checkpoints.Add(e);
        }
    }
}
