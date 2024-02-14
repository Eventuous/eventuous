using Eventuous.Diagnostics.Logging;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Subscriptions;

public abstract class LegacySubscriptionFixture<T> : IClassFixture<StoreFixture>, IAsyncLifetime where T : class, IEventHandler {
    static LegacySubscriptionFixture() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected readonly Fixture Auto = new();

    protected StreamName          Stream          { get; } = new($"test-{Guid.NewGuid():N}");
    public    StoreFixture        StoreFixture    { get; }
    protected T                   Handler         { get; }
    protected EventStoreProducer  Producer        { get; private set; } = null!;
    protected ILogger             Log             { get; }
    protected TestCheckpointStore CheckpointStore { get; }
    protected StreamSubscription  Subscription    { get; set; } = null!;

    protected LegacySubscriptionFixture(
            StoreFixture      storeFixture,
            ITestOutputHelper outputHelper,
            T                 handler,
            bool              autoStart = true,
            StreamName?       stream    = null,
            LogLevel          logLevel  = LogLevel.Debug
        ) {
        _autoStart = autoStart;
        if (stream is { } s) Stream = s;

        LoggerFactory = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);

        StoreFixture    = storeFixture;
        Handler         = handler;
        Log             = LoggerFactory.CreateLogger(GetType());
        CheckpointStore = new TestCheckpointStore();

        _listener = new LoggingEventListener(LoggerFactory);
    }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);
    ILoggerFactory LoggerFactory { get; }

    readonly bool                 _autoStart;
    readonly LoggingEventListener _listener;

    public async Task InitializeAsync() {
        Producer = new EventStoreProducer(StoreFixture.Client);

        var subscriptionId = $"test-{Guid.NewGuid():N}";
        var pipe           = new ConsumePipe();
        pipe.AddDefaultConsumer(Handler);

        Subscription = new StreamSubscription(
            StoreFixture.Client,
            new StreamSubscriptionOptions {
                StreamName     = Stream,
                SubscriptionId = subscriptionId,
                ResolveLinkTos = Stream.ToString().StartsWith('$')
            },
            CheckpointStore,
            pipe,
            LoggerFactory
        );
        if (_autoStart) await Start();
    }

    public async Task DisposeAsync() {
        if (_autoStart) await Stop();
        _listener.Dispose();
    }
}
