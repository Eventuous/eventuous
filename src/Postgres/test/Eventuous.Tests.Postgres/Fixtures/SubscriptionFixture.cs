using Eventuous.Diagnostics.Logging;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.Postgres.Fixtures;

public abstract class SubscriptionFixture<T> : IAsyncLifetime
    where T : class, IEventHandler {
    static SubscriptionFixture()
        => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected IntegrationFixture      IntegrationFixture { get; } = new();
    protected StreamName              Stream             { get; }
    protected T                       Handler            { get; }
    protected ILogger                 Log                { get; }
    protected PostgresCheckpointStore CheckpointStore    { get; }
    IMessageSubscription              Subscription       { get; }
    protected ILoggerFactory          LoggerFactory      { get; }

    protected SubscriptionFixture(
        ITestOutputHelper    outputHelper,
        bool                 subscribeToAll,
        bool                 autoStart     = true,
        Action<ConsumePipe>? configurePipe = null,
        LogLevel             logLevel      = LogLevel.Trace
    ) {
        _autoStart = autoStart;

        Stream = new StreamName(SharedAutoFixture.Auto.Create<string>());

        _schema = new Schema(IntegrationFixture.SchemaName);

        LoggerFactory  = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);
        SubscriptionId = $"test-{Guid.NewGuid():N}";

        Handler         = GetHandler();
        Log             = LoggerFactory.CreateLogger(GetType());
        CheckpointStore = new PostgresCheckpointStore(IntegrationFixture.DataSource, IntegrationFixture.SchemaName, LoggerFactory);

        _listener = new LoggingEventListener(LoggerFactory);
        var pipe = new ConsumePipe();
        configurePipe?.Invoke(pipe);
        pipe.AddDefaultConsumer(Handler);

        Subscription =
            !subscribeToAll
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
    }

    protected abstract T GetHandler();

    public string SubscriptionId { get; }

    protected ValueTask Start()
        => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop()
        => Subscription.UnsubscribeWithLog(Log);

    readonly bool                 _autoStart;
    readonly LoggingEventListener _listener;
    readonly Schema               _schema;

    public async Task InitializeAsync() {
        await _schema.CreateSchema(IntegrationFixture.DataSource);
        if (_autoStart) await Start();
    }

    public async Task DisposeAsync() {
        if (_autoStart) await Stop();
        await DropSchema();
        _listener.Dispose();
        await IntegrationFixture.DisposeAsync();
    }

    async Task DropSchema() {
        await using var dataSource = IntegrationFixture.DataSource;
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"drop schema if exists {IntegrationFixture.SchemaName} cascade;";
        await cmd.ExecuteNonQueryAsync();
    }
}