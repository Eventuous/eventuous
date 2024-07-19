using Eventuous.Diagnostics.Logging;
using Eventuous.Redis.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.Redis.Fixtures;

public abstract class SubscriptionFixture<T> : IAsyncLifetime where T : class, IEventHandler {
    static SubscriptionFixture() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected IntegrationFixture   IntegrationFixture { get; private set; } = null!;
    protected StreamName           Stream             { get; }
    protected T                    Handler            { get; private set; } = null!;
    protected ILogger              Log                { get; }
    protected RedisCheckpointStore CheckpointStore    { get; private set; } = null!;
    protected ILoggerFactory       LoggerFactory      { get; }
    IMessageSubscription           Subscription       { get; set; } = null!;

    protected SubscriptionFixture(ITestOutputHelper outputHelper, bool subscribeToAll, bool autoStart = true, LogLevel logLevel = LogLevel.Trace) {
        _subscribeToAll = subscribeToAll;
        _autoStart      = autoStart;
        Stream          = new StreamName(SharedAutoFixture.Auto.Create<string>());
        LoggerFactory   = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);
        SubscriptionId  = $"test-{Guid.NewGuid():N}";
        Log             = LoggerFactory.CreateLogger(GetType());
        _listener       = new LoggingEventListener(LoggerFactory);
    }

    protected abstract T GetHandler();

    public string SubscriptionId { get; }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    readonly bool                 _subscribeToAll;
    readonly bool                 _autoStart;
    readonly LoggingEventListener _listener;

    public async Task InitializeAsync() {
        IntegrationFixture = new();
        await IntegrationFixture.InitializeAsync();
        Handler         = GetHandler();
        CheckpointStore = new RedisCheckpointStore(IntegrationFixture.GetDatabase, LoggerFactory);

        var pipe = new ConsumePipe();
        pipe.AddDefaultConsumer(Handler);

        Subscription =
            !_subscribeToAll
                ? new RedisStreamSubscription(
                    IntegrationFixture.GetDatabase,
                    new RedisStreamSubscriptionOptions(Stream) { SubscriptionId = SubscriptionId },
                    CheckpointStore,
                    pipe,
                    LoggerFactory
                )
                : new RedisAllStreamSubscription(
                    IntegrationFixture.GetDatabase,
                    new RedisAllStreamSubscriptionOptions { SubscriptionId = SubscriptionId },
                    CheckpointStore,
                    pipe,
                    LoggerFactory
                );
        if (_autoStart) await Start();
    }

    public async Task DisposeAsync() {
        if (_autoStart) await Stop();
        await FlushDB();
        _listener.Dispose();
        await IntegrationFixture.DisposeAsync();
    }

    async Task FlushDB() {
        var database = IntegrationFixture.GetDatabase();
        await database.ExecuteAsync("FLUSHDB");
    }
}
