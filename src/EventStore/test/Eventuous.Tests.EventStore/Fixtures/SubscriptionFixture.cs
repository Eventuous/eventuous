using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Fixtures;

public abstract class SubscriptionFixture<T> : IAsyncLifetime where T : class, IEventHandler {
    static SubscriptionFixture()
        => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected static readonly Fixture Auto = new();

    protected StreamName          Stream          { get; } = new($"test-{Guid.NewGuid():N}");
    protected T                   Handler         { get; }
    protected EventStoreProducer  Producer        { get; }
    protected ILogger             Log             { get; }
    protected TestCheckpointStore CheckpointStore { get; }
    StreamSubscription            Subscription    { get; }

    protected SubscriptionFixture(
        ITestOutputHelper outputHelper,
        T                 handler,
        bool              autoStart = true
    ) {
        _autoStart = autoStart;

        var loggerFactory  = TestHelpers.Logging.GetLoggerFactory(outputHelper);
        var subscriptionId = $"test-{Guid.NewGuid():N}";

        Handler         = handler;
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
            new TracedConsumer(
                new DefaultConsumer(new IEventHandler[] { Handler }, true)
            ),
            loggerFactory
        );
    }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    readonly bool _autoStart;

    public async Task InitializeAsync() {
        if (_autoStart) await Start();
    }

    public async Task DisposeAsync() {
        if (_autoStart) await Stop();
    }
}