using Eventuous.Diagnostics.Logging;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;
using static Eventuous.Tests.Postgres.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Postgres.Subscriptions;

public abstract class SubscriptionFixture<T> : IAsyncLifetime
    where T : class, IEventHandler {
    static SubscriptionFixture() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected readonly Fixture Auto = new();

    protected StreamName              Stream          { get; }
    protected T                       Handler         { get; }
    protected ILogger                 Log             { get; }
    protected PostgresCheckpointStore CheckpointStore { get; }
    IMessageSubscription              Subscription    { get; }
    protected string                  SchemaName      { get; }

    protected SubscriptionFixture(
        ITestOutputHelper    outputHelper,
        T                    handler,
        bool                 subscribeToAll,
        bool                 autoStart     = true,
        Action<ConsumePipe>? configurePipe = null,
        LogLevel             logLevel      = LogLevel.Debug
    ) {
        _autoStart = autoStart;

        Stream = new StreamName(Instance.Auto.Create<string>());

        SchemaName = Instance.SchemaName;
        _schema    = new Schema(SchemaName);

        var loggerFactory = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);
        SubscriptionId = $"test-{Guid.NewGuid():N}";

        Handler         = handler;
        Log             = loggerFactory.CreateLogger(GetType());
        CheckpointStore = new PostgresCheckpointStore(Instance.GetConnection, SchemaName);

        _listener = new LoggingEventListener(loggerFactory);
        var pipe = new ConsumePipe();
        configurePipe?.Invoke(pipe);
        pipe.AddDefaultConsumer(Handler);

        Subscription =
            !subscribeToAll
                ? new PostgresStreamSubscription(
                    Instance.GetConnection,
                    new PostgresStreamSubscriptionOptions(Stream) {
                        SubscriptionId = SubscriptionId,
                        Schema         = SchemaName
                    },
                    CheckpointStore,
                    pipe
                )
                : new PostgresAllStreamSubscription(
                    Instance.GetConnection,
                    new PostgresAllStreamSubscriptionOptions {
                        SubscriptionId = SubscriptionId,
                        Schema         = SchemaName
                    },
                    CheckpointStore,
                    pipe
                );
    }

    public string SubscriptionId { get; }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    readonly bool                 _autoStart;
    readonly LoggingEventListener _listener;
    readonly Schema               _schema;

    public async Task InitializeAsync() {
        await _schema.CreateSchema(Instance.GetConnection);
        if (_autoStart) await Start();
    }

    public async Task DisposeAsync() {
        if (_autoStart) await Stop();
        _listener.Dispose();
    }
}
