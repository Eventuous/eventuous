using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.DependencyInjection;
// ReSharper disable MethodHasAsyncOverload

namespace Eventuous.Tests.EventStore.Subscriptions;

public class SubscriptionIgnoredMessagesTests : StoreFixture {
    readonly string     _subscriptionId  = $"test-{Guid.NewGuid():N}";
    readonly StreamName _stream          = new($"test-{Guid.NewGuid():N}");
    IProducer           _producer        = null!;
    ICheckpointStore    _checkpointStore = null!;
    TestEventHandler    _handler         = null!;

    public SubscriptionIgnoredMessagesTests() {
        AutoStart = false;
    }

    [Test]
    [Category("Special cases")]
    public async Task SubscribeAndProduceManyWithIgnored(CancellationToken cancellationToken) {
        const int count = 10;

        var testEvents = Generate().ToList();

        TypeMapper.AddType<TestEvent>(TestEvent.TypeName);
        TypeMapper.AddType<UnknownEvent>("ignored");
        TestContext.Current?.OutputWriter.WriteLine($"Producing to {_stream}");
        await _producer.Produce(_stream, testEvents, new Metadata(), cancellationToken: cancellationToken);
        TestContext.Current?.OutputWriter.WriteLine("Produce complete");

        TypeMapper.RemoveType<UnknownEvent>();

        var expected = testEvents.Where(x => x.GetType() == typeof(TestEvent)).ToList();
        await Start();
        await _handler.AssertCollection(5.Seconds(), expected).Validate(cancellationToken);
        await DisposeAsync();

        var last = await _checkpointStore.GetLastCheckpoint(_subscriptionId, cancellationToken);
        last.Position.Should().Be((ulong)(testEvents.Count - 1));

        return;

        IEnumerable<object> Generate() {
            for (var i = 0; i < count; i++) {
                yield return new TestEvent(Guid.NewGuid().ToString(), i);
                yield return new UnknownEvent(Guid.NewGuid().ToString(), i);
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
        _producer        = provider.GetRequiredService<IProducer>();
        _checkpointStore = provider.GetRequiredKeyedService<TestCheckpointStore>(_subscriptionId);
        _handler         = provider.GetRequiredKeyedService<TestEventHandler>(_subscriptionId);
    }

    record UnknownEvent(string Data, int Number);
}
