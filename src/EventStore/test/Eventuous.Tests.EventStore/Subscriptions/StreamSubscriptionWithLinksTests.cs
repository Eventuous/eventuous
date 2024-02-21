using Bogus;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Context;
using Eventuous.Sut.Subs;
using Eventuous.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class StreamSubscriptionWithLinksTests : IAsyncLifetime {
    const string SubId = "Test";

    public StreamSubscriptionWithLinksTests(ITestOutputHelper output) {
        _fixture = new StoreFixture();
        _output  = output;
        _prefix  = $"{Faker.Commerce.ProductAdjective()}{Faker.Commerce.Product()}";
        output.WriteLine($"Stream prefix: {_prefix}");
    }

    readonly List<Checkpoint>  _checkpoints = [];
    readonly StoreFixture      _fixture;
    readonly ITestOutputHelper _output;
    readonly string            _prefix;
    readonly List<TestEvent>   _events = [];
    readonly ServiceCollection _services = [];

    Faker Faker { get; } = new();

    async Task Seed(IServiceProvider provider, int count) {
        TypeMap.Instance.AddType<TestEvent>(TestEvent.TypeName);
        var producer = provider.GetRequiredService<IEventProducer>();

        _output.WriteLine("Producing events...");

        for (var i = 0; i < count; i++) {
            var evt    = _fixture.Auto.Create<TestEvent>();
            var stream = new StreamName($"{_prefix}-{_fixture.Auto.Create<string>()}");
            await producer.Produce(stream, evt, null);
            _events.Add(evt);
        }

        _output.WriteLine("Producing complete");
    }

    void AddCheckpointStore(ulong? start) {
        var checkpointStore = new NoOpCheckpointStore(start);
        checkpointStore.CheckpointStored += CheckpointStoreOnCheckpointStored;
        _services.AddSingleton<ICheckpointStore>(checkpointStore);

        return;

        void CheckpointStoreOnCheckpointStored(object? sender, Checkpoint e) {
            _output.WriteLine($"Stored checkpoint {e.Id}: {e.Position}");
            _checkpoints.Add(e);
        }
    }

    IServiceProvider Build() {
        var provider = _services.BuildServiceProvider();
        provider.AddEventuousLogs();

        return provider;
    }

    static IHostedService[] GetHostedServices(IServiceProvider provider)
        => provider.GetServices<IHostedService>().ToArray();

    [Fact]
    public async Task ShouldHandleAllEventsFromStart() {
        await Execute(5000, null);
    }

    [Fact]
    public async Task ShouldHandleHalfOfTheEvents() {
        const int count = 1000;
        await Execute(count, count / 2);
    }

    async Task Execute(int count, ulong? start) {
        AddCheckpointStore(start);
        var provider = Build();
        await Seed(provider, count);
        var services = await StartServices(provider);

        await WaitForCheckpoint(count, 10.Seconds());

        ValidateProcessed(provider, start == null ? _events : _events.Skip((int)start.Value));

        await StopServices(services);
        ValidateCheckpoint(count);
    }

    static void ValidateProcessed(IServiceProvider provider, IEnumerable<TestEvent> events) {
        var handler = provider.GetRequiredService<TestHandler>();
        var diff    = handler.Handled.Except(events);
        diff.Should().BeEmpty();
    }

    void ValidateCheckpoint(int count) {
        _checkpoints.Count.Should().BeGreaterThan(0);
        _checkpoints.Skip(1).Select(x => x.Position).Should().NotContain(0);
        _checkpoints.Last().Position.Should().Be((ulong)(count - 1));
    }

    async Task WaitForCheckpoint(int count, TimeSpan deadline) {
        var source = new CancellationTokenSource(deadline);

        var expected = (ulong)(count - 1);

        try {
            var last = _checkpoints.LastOrDefault().Position;

            if (last == expected) return;

            await Task.Delay(500, source.Token);
        } catch (OperationCanceledException) {
            _output.WriteLine("Deadline exceeded");
        }
    }

    async Task<IHostedService[]> StartServices(IServiceProvider provider) {
        var services = GetHostedServices(provider);

        await services.Select(
                async x => {
                    _output.WriteLine($"Starting service {x.GetType().Name}");
                    await x.StartAsync(default);
                }
            )
            .WhenAll();

        return services;
    }

    async Task StopServices(IHostedService[] services) {
        await services.Select(x => x.StopAsync(default)).WhenAll();
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    class TestHandler(ILogger<TestHandler> logger) : BaseEventHandler {
        public List<object> Handled { get; } = new();

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext ctx) {
            Handled.Add(ctx.Message!);

            logger.LogDebug("Handled event from {Stream} at {Position}", ctx.Stream, ctx.StreamPosition);

            return ValueTask.FromResult(EventHandlingStatus.Success);
        }
    }

    public async Task InitializeAsync() {
        await _fixture.InitializeAsync();

        _services.AddLogging(cfg => cfg.AddXunit(_output).SetMinimumLevel(LogLevel.Information));
        _services.AddSingleton(_fixture.Client);
        _services.AddProducer<EventStoreProducer>();

        _services
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
    }

    public async Task DisposeAsync() {
        await _fixture.DisposeAsync();
    }
}
