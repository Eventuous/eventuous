using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.EventStore.Subscriptions.Fixtures;

public abstract class LegacySubscriptionFixture<T> : IAsyncLifetime where T : class, IEventHandler {
    protected readonly Fixture Auto = new();

    protected StreamName          Stream          { get; } = new($"test-{Guid.NewGuid():N}");
    protected StoreFixture        StoreFixture    { get; } = new();
    protected T                   Handler         { get; }
    protected EventStoreProducer  Producer        { get; private set; } = null!;
    protected ILogger             Log             { get; }
    protected TestCheckpointStore CheckpointStore { get; }      = new();
    protected StreamSubscription  Subscription    { get; set; } = null!;

    protected LegacySubscriptionFixture(
            ITestOutputHelper output,
            T                 handler,
            bool              autoStart = true,
            StreamName?       stream    = null,
            LogLevel          logLevel  = LogLevel.Debug
        ) {
        _autoStart = autoStart;
        if (stream is { } s) Stream = s;

        LoggerFactory = TestHelpers.Logging.GetLoggerFactory(output, logLevel);
        Handler       = handler;
        Log           = LoggerFactory.CreateLogger(GetType());
        StoreFixture.TypeMapper.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
    }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);
    ILoggerFactory LoggerFactory { get; }

    readonly bool _autoStart;

    public async Task InitializeAsync() {
        await StoreFixture.InitializeAsync();
        Producer = new(StoreFixture.Client);

        var subscriptionId = $"test-{Guid.NewGuid():N}";
        var pipe           = new ConsumePipe();
        pipe.AddDefaultConsumer(Handler);

        Subscription = new(
            StoreFixture.Client,
            new() {
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
        await StoreFixture.DisposeAsync();
    }
}
