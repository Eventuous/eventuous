using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Fixtures;

public abstract class SubscriptionFixture : IAsyncLifetime {
    static SubscriptionFixture() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
    
    protected static readonly Fixture Auto = new();

    public StreamName          Stream          { get; } = new($"test-{Guid.NewGuid():N}");
    public TestEventHandler    Handler         { get; }
    public EventStoreProducer  Producer        { get; }
    public ILogger             Log             { get; }
    public TestCheckpointStore CheckpointStore { get; }
    public StreamSubscription  Subscription    { get; }

    public SubscriptionFixture(ITestOutputHelper outputHelper, bool autoStart = true) {
        _autoStart = autoStart;

        var loggerFactory  = Logging.GetLoggerFactory(outputHelper);
        var subscriptionId = $"test-{Guid.NewGuid():N}";

        Handler         = new TestEventHandler();
        Producer        = new EventStoreProducer(IntegrationFixture.Instance.Client);
        Log             = loggerFactory.CreateLogger(GetType());
        CheckpointStore = new TestCheckpointStore();

        Subscription = new StreamSubscription(
            IntegrationFixture.Instance.Client,
            new StreamSubscriptionOptions {
                StreamName     = Stream,
                SubscriptionId = subscriptionId
            },
            CheckpointStore,
            new DefaultConsumer(new IEventHandler[] { Handler }, true),
            loggerFactory
        );
    }

    public ValueTask Start() => Subscription.SubscribeWithLog(Log);

    public ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    readonly bool _autoStart;

    public async Task InitializeAsync() {
        if (_autoStart) await Start();
    }

    public async Task DisposeAsync() {
        if (_autoStart) await Stop();
    }
}