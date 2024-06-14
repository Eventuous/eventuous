// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class SubscriptionIgnoredMessagesTests : StoreFixture {
    ICheckpointStore    _checkpointStore = null!;
    readonly string     _subscriptionId  = $"test-{Guid.NewGuid():N}";
    readonly StreamName _stream          = new($"test-{Guid.NewGuid():N}");
    IEventProducer      _producer        = null!;
    TestEventHandler    _handler         = null!;

    public SubscriptionIgnoredMessagesTests(ITestOutputHelper output) {
        Output    = output;
        AutoStart = false;
    }

    [Fact]
    [Trait("Category", "Special cases")]
    public async Task SubscribeAndProduceManyWithIgnored() {
        const int count = 10;

        var testEvents = Generate().ToList();

        TypeMap.Instance.AddType<TestEvent>(TestEvent.TypeName);
        TypeMap.Instance.AddType<UnknownEvent>("ignored");
        Output?.WriteLine($"Producing to {_stream}");
        await _producer.Produce(_stream, testEvents, new Metadata());
        Output?.WriteLine("Produce complete");

        TypeMap.Instance.RemoveType<UnknownEvent>();

        var expected = testEvents.Where(x => x.GetType() == typeof(TestEvent)).ToList();
        await Start();
        await _handler.AssertCollection(5.Seconds(), expected).Validate();
        await DisposeAsync();

        var last = await _checkpointStore.GetLastCheckpoint(_subscriptionId, default);
        last.Position.Should().Be((ulong)(testEvents.Count - 1));

        return;

        IEnumerable<object> Generate() {
            for (var i = 0; i < count; i++) {
                yield return new TestEvent(Auto.Create<string>(), i);
                yield return new UnknownEvent(Auto.Create<string>(), i);
            }
        }
    }

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);
        services.AddProducer<EventStoreProducer>();

        services.AddSubscription<StreamSubscription, StreamSubscriptionOptions>(
            _subscriptionId,
            c => c
                .Configure(o => o.StreamName = _stream)
                .UseCheckpointStore<TestCheckpointStore>()
                .AddEventHandler<TestEventHandler>()
        );
    }

    protected override void GetDependencies(IServiceProvider provider) {
        base.GetDependencies(provider);
        _producer        = provider.GetRequiredService<IEventProducer>();
        _checkpointStore = provider.GetRequiredKeyedService<TestCheckpointStore>(_subscriptionId);
        _handler         = provider.GetRequiredKeyedService<TestEventHandler>(_subscriptionId);
    }

    record UnknownEvent(string Data, int Number);
}
