using Eventuous.Diagnostics.Logging;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.Postgres.Fixtures;

public abstract class SubscriptionFixture<T> : IAsyncLifetime where T : class, IEventHandler {
    static SubscriptionFixture()
        => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected IntegrationFixture      IntegrationFixture { get; } = new();
    protected StreamName              Stream             { get; }
    protected T                       Handler            { get; private set; } = null!;
    protected ILogger                 Log                { get; }
    protected PostgresCheckpointStore CheckpointStore    { get; private set; } = null!;
    IMessageSubscription              Subscription       { get; set; }         = null!;
    protected ILoggerFactory          LoggerFactory      { get; }

    protected SubscriptionFixture(
            ITestOutputHelper    outputHelper,
            bool                 subscribeToAll,
            bool                 autoStart     = true,
            Action<ConsumePipe>? configurePipe = null,
            LogLevel             logLevel      = LogLevel.Trace
        ) {
        _subscribeToAll = subscribeToAll;
        _autoStart      = autoStart;
        _configurePipe  = configurePipe;

        Stream = new StreamName(SharedAutoFixture.Auto.Create<string>());

        _schema = new Schema(IntegrationFixture.SchemaName);

        LoggerFactory  = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);
        SubscriptionId = $"test-{Guid.NewGuid():N}";

        Log = LoggerFactory.CreateLogger(GetType());

        _listener = new LoggingEventListener(LoggerFactory);
    }

    protected abstract T GetHandler();

    public string SubscriptionId { get; }

    protected ValueTask Start()
        => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop()
        => Subscription.UnsubscribeWithLog(Log);

    readonly bool                 _subscribeToAll;
    readonly bool                 _autoStart;
    readonly Action<ConsumePipe>? _configurePipe;
    readonly LoggingEventListener _listener;
    readonly Schema               _schema;

    public async Task InitializeAsync() {
        await IntegrationFixture.InitializeAsync();
        await _schema.CreateSchema(IntegrationFixture.DataSource, LoggerFactory.CreateLogger<Schema>());
        CheckpointStore = new PostgresCheckpointStore(IntegrationFixture.DataSource, IntegrationFixture.SchemaName, LoggerFactory);
        var pipe = new ConsumePipe();
        _configurePipe?.Invoke(pipe);
        Handler = GetHandler();
        pipe.AddDefaultConsumer(Handler);

        Subscription =
            !_subscribeToAll
                ? new PostgresStreamSubscription(
                    IntegrationFixture.DataSource,
                    new PostgresStreamSubscriptionOptions {
                        Stream         = Stream,
                        SubscriptionId = SubscriptionId,
                        Schema         = IntegrationFixture.SchemaName
                    },
                    CheckpointStore,
                    pipe,
                    LoggerFactory
                )
                : new PostgresAllStreamSubscription(
                    IntegrationFixture.DataSource,
                    new PostgresAllStreamSubscriptionOptions {
                        SubscriptionId = SubscriptionId,
                        Schema         = IntegrationFixture.SchemaName
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
        await IntegrationFixture.DisposeAsync();
    }
}
