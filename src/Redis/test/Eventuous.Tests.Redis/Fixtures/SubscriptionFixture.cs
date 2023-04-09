using Eventuous.Diagnostics.Logging;
using Eventuous.Redis.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.Redis.Fixtures;

public abstract class SubscriptionFixture<T> : IAsyncLifetime
    where T : class, IEventHandler {
    static SubscriptionFixture()
        => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected IntegrationFixture   IntegrationFixture { get; } = new();
    protected StreamName           Stream             { get; }
    protected T                    Handler            { get; }
    protected ILogger              Log                { get; }
    protected RedisCheckpointStore CheckpointStore    { get; }
    IMessageSubscription           Subscription       { get; }
    protected ILoggerFactory       LoggerFactory      { get; }

    protected SubscriptionFixture(
        ITestOutputHelper    outputHelper,
        bool                 subscribeToAll,
        bool                 autoStart     = true,
        Action<ConsumePipe>? configurePipe = null,
        LogLevel             logLevel      = LogLevel.Trace
    ) {
        _autoStart = autoStart;

        Stream = new StreamName(SharedAutoFixture.Auto.Create<string>());

        LoggerFactory  = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);
        SubscriptionId = $"test-{Guid.NewGuid():N}";

        Handler         = GetHandler();
        Log             = LoggerFactory.CreateLogger(GetType());
        CheckpointStore = new RedisCheckpointStore(IntegrationFixture.GetDatabase, LoggerFactory);

        _listener = new LoggingEventListener(LoggerFactory);
        var pipe = new ConsumePipe();
        configurePipe?.Invoke(pipe);
        pipe.AddDefaultConsumer(Handler);

        Subscription =
            !subscribeToAll
                ? new RedisStreamSubscription(
                    IntegrationFixture.GetDatabase,
                    new RedisStreamSubscriptionOptions(Stream) {
                        SubscriptionId = SubscriptionId
                    },
                    CheckpointStore,
                    pipe,
                    LoggerFactory
                )
                : new RedisAllStreamSubscription(
                    IntegrationFixture.GetDatabase,
                    new RedisAllStreamSubscriptionOptions {
                        SubscriptionId = SubscriptionId
                    },
                    CheckpointStore,
                    pipe,
                    LoggerFactory
                );
    }

    protected abstract T GetHandler();

    public string SubscriptionId { get; }

    protected ValueTask Start()
        => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop()
        => Subscription.UnsubscribeWithLog(Log);

    readonly bool                 _autoStart;
    readonly LoggingEventListener _listener;

    public async Task InitializeAsync() {
        if (_autoStart) await Start();
    }

    public async Task DisposeAsync() {
        if (_autoStart) await Stop();
        await FlashDB();
        _listener.Dispose();
        await IntegrationFixture.DisposeAsync();
    }

    async Task FlashDB() {
        var database = IntegrationFixture.GetDatabase();
        await database.ExecuteAsync("FLUSHDB");
    }
}
