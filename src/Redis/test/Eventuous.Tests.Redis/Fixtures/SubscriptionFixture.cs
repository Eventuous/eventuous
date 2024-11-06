using Eventuous.Diagnostics.Logging;
using Eventuous.Redis.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.Redis.Fixtures;

public class SubscriptionFixture<T> where T : IEventHandler, new() {
    static SubscriptionFixture() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    public    IntegrationFixture   IntegrationFixture { get; private set; } = null!;
    public    StreamName           Stream             { get; }
    protected ILogger              Log                { get; }
    public    RedisCheckpointStore CheckpointStore    { get; private set; } = null!;
    public    ILoggerFactory       LoggerFactory      { get; }
    public    T                    Handler            { get; }
    IMessageSubscription           Subscription       { get; set; } = null!;

    public SubscriptionFixture(bool subscribeToAll, LogLevel logLevel = LogLevel.Trace) {
        Handler         = new T();
        _subscribeToAll = subscribeToAll;
        Stream          = new(SharedAutoFixture.Auto.Create<string>());
        LoggerFactory   = LoggingExtensions.GetLoggerFactory(logLevel);
        SubscriptionId  = $"test-{Guid.NewGuid():N}";
        Log             = LoggerFactory.CreateLogger(GetType());
        _listener       = new(LoggerFactory);
    }

    public string SubscriptionId { get; }

    public async Task Start() {
        await Subscription.SubscribeWithLog(Log);
    }

    public async Task Stop() {
        await Subscription.UnsubscribeWithLog(Log);
    }

    readonly bool                 _subscribeToAll;
    readonly LoggingEventListener _listener;

    public async ValueTask InitializeAsync() {
        IntegrationFixture = new();
        await IntegrationFixture.InitializeAsync();
        CheckpointStore = new(IntegrationFixture.GetDatabase, LoggerFactory);

        var pipe = new ConsumePipe();
        pipe.AddDefaultConsumer(Handler);

        Subscription =
            !_subscribeToAll
                ? new RedisStreamSubscription(
                    IntegrationFixture.GetDatabase,
                    new(Stream) { SubscriptionId = SubscriptionId },
                    CheckpointStore,
                    pipe,
                    LoggerFactory
                )
                : new RedisAllStreamSubscription(
                    IntegrationFixture.GetDatabase,
                    new() { SubscriptionId = SubscriptionId },
                    CheckpointStore,
                    pipe,
                    LoggerFactory
                );
    }

    public async ValueTask DisposeAsync() {
        await FlushDb();
        _listener.Dispose();
        await IntegrationFixture.DisposeAsync();
    }

    async Task FlushDb() {
        var database = IntegrationFixture.GetDatabase();
        await database.ExecuteAsync("FLUSHDB");
    }
}
