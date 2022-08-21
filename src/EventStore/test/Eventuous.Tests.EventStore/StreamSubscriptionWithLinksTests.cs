// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Bogus;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Context;
using Eventuous.Sut.Subs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Eventuous.Tests.EventStore.Fixtures.IntegrationFixture;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.EventStore;

public class StreamSubscriptionWithLinksTests : IAsyncLifetime {
    const string SubId = "Test";

    public StreamSubscriptionWithLinksTests(ITestOutputHelper output) {
        _output = output;
        _prefix = Faker.Commerce.Product();
        output.WriteLine($"Stream prefix: {_prefix}");

        var services = new ServiceCollection();

        services.AddLogging(cfg => cfg.AddXunit(output));
        services.AddSingleton(Instance.Client);
        var checkpointStore = new NoOpCheckpointStore();
        checkpointStore.CheckpointStored += CheckpointStoreOnCheckpointStored;
        services.AddSingleton<ICheckpointStore>(checkpointStore);
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

        Provider  = services.BuildServiceProvider();
        _services = Provider.GetServices<IHostedService>();

        void CheckpointStoreOnCheckpointStored(object? sender, Checkpoint e) {
            output.WriteLine($"Stored checkpoint: {e.Position}");
            _checkpoints.Add(e);
        }
    }

    readonly List<Checkpoint>            _checkpoints = new();
    readonly IEnumerable<IHostedService> _services;
    readonly ITestOutputHelper           _output;
    readonly string                      _prefix;
    readonly List<TestEvent>             _events = new();

    ServiceProvider Provider { get; }
    Faker           Faker    { get; } = new();

    const int TotalCount = 10000;

    public async Task InitializeAsync() {
        var producer = Provider.GetRequiredService<IEventProducer>();

        _output.WriteLine("Producing events...");

        for (var i = 0; i < TotalCount; i++) {
            var evt    = Instance.Auto.Create<TestEvent>();
            var stream = new StreamName($"{_prefix}-{Instance.Auto.Create<string>()}");
            await producer.Produce(stream, evt, null);
            _events.Add(evt);
        }

        _output.WriteLine("Producing complete");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ShouldReceive10KEvents() {
        await _services.Select(
                async x => {
                    _output.WriteLine($"Starting service {x.GetType().Name}");
                    await x.StartAsync(default);
                }
            )
            .WhenAll();
        
        await Task.Delay(5000);
        var handler = Provider.GetRequiredService<TestHandler>();
        var diff    = handler.Handled.Except(_events);
        diff.Should().BeEmpty();
        _output.WriteLine($"Checkpoints stored {_checkpoints.Count} times");
        _checkpoints.Count.Should().BeGreaterThan(0);
        _checkpoints.Skip(1).Select(x => x.Position).Should().NotContain(0);
        
        await _services.Select(x => x.StopAsync(default)).WhenAll();

        _checkpoints.Last().Position.Should().Be(TotalCount - 1);
    }

    class TestHandler : BaseEventHandler {
        public List<object> Handled { get; } = new();

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext ctx) {
            Handled.Add(ctx.Message!);
            return ValueTask.FromResult(EventHandlingStatus.Success);
        }
    }
}
