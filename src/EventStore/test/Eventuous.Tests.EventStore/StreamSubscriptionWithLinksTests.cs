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

namespace Eventuous.Tests.EventStore;

public class StreamSubscriptionWithLinksTests : IClassFixture<IntegrationFixture> {
    const string SubId = "Test";

    public StreamSubscriptionWithLinksTests(IntegrationFixture fixture, ITestOutputHelper output) {
        _fixture = fixture;
        _output  = output;
        _prefix  = $"{Faker.Commerce.ProductAdjective()}{Faker.Commerce.Product()}";
        output.WriteLine($"Stream prefix: {_prefix}");

        var services = new ServiceCollection();

        services.AddLogging(cfg => cfg.AddXunit(output).SetMinimumLevel(LogLevel.Information));
        services.AddSingleton(fixture.Client);
        services.AddEventProducer<EventStoreProducer>();

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

        _services = services;
    }

    readonly List<Checkpoint>   _checkpoints = new();
    readonly IntegrationFixture _fixture;
    readonly ITestOutputHelper  _output;
    readonly string             _prefix;
    readonly List<TestEvent>    _events = new();
    readonly ServiceCollection  _services;

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
        const int count = 5000;

        AddCheckpointStore(null);
        var provider = Build();
        await Seed(provider, count);
        var services = GetHostedServices(provider);

        await services.Select(
                async x => {
                    _output.WriteLine($"Starting service {x.GetType().Name}");
                    await x.StartAsync(default);
                }
            )
            .WhenAll();

        await Task.Delay(1000);
        var handler = provider.GetRequiredService<TestHandler>();
        var diff    = handler.Handled.Except(_events);
        diff.Should().BeEmpty();
        _output.WriteLine($"Checkpoints stored {_checkpoints.Count} times");
        _checkpoints.Count.Should().BeGreaterThan(0);
        _checkpoints.Skip(1).Select(x => x.Position).Should().NotContain(0);

        await services.Select(x => x.StopAsync(default)).WhenAll();

        _checkpoints.Last().Position.Should().Be(count - 1);
    }

    [Fact]
    public async Task ShouldHandleHalfOfTheEvents() {
        const int count = 1000;

        AddCheckpointStore(count / 2);
        var provider = Build();
        await Seed(provider, count);
        var services = GetHostedServices(provider);

        await services.Select(
                async x => {
                    _output.WriteLine($"Starting service {x.GetType().Name}");
                    await x.StartAsync(default);
                }
            )
            .WhenAll();

        await Task.Delay(1000);
        var handler = provider.GetRequiredService<TestHandler>();
        var diff    = handler.Handled.Except(_events.Skip(count / 2));
        diff.Should().BeEmpty();
        _output.WriteLine($"Checkpoints stored {_checkpoints.Count} times");
        _checkpoints.Count.Should().BeGreaterThan(0);
        _checkpoints.Skip(1).Select(x => x.Position).Should().NotContain(0);

        await services.Select(x => x.StopAsync(default)).WhenAll();

        _checkpoints.Last().Position.Should().Be(count - 1);
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    class TestHandler(ILogger<TestHandler> logger) : BaseEventHandler {
        public List<object> Handled { get; } = new();

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext ctx) {
            Handled.Add(ctx.Message!);

            logger.LogInformation("Handled event from {Stream} at {Position}", ctx.Stream, ctx.StreamPosition);

            return ValueTask.FromResult(EventHandlingStatus.Success);
        }
    }
}
