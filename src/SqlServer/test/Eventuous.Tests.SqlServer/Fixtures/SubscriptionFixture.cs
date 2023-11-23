using Eventuous.Diagnostics.Logging;
using Eventuous.SqlServer;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.SqlServer.Fixtures;

public abstract class SubscriptionFixture<T> : IAsyncLifetime where T : class, IEventHandler {
    static SubscriptionFixture() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected StreamName               Stream                 { get; }
    protected T                        Handler                { get; }
    protected ILogger                  Log                    { get; }
    protected SqlServerCheckpointStore CheckpointStore        { get; private set; } = null!;
    SqlServerCheckpointStoreOptions    CheckpointStoreOptions { get; set; }         = null!;
    IMessageSubscription               Subscription           { get; set; }         = null!;
    protected string                   SchemaName             { get; }

    protected SubscriptionFixture(
            ITestOutputHelper  outputHelper,
            T                  handler,
            bool               subscribeToAll,
            bool               autoStart = true,
            LogLevel           logLevel  = LogLevel.Debug
        ) {
        _autoStart      = autoStart;
        Fixture         = new IntegrationFixture();
        _subscribeToAll = subscribeToAll;
        Stream          = new StreamName(Fixture.Auto.Create<string>());
        SchemaName      = Fixture.SchemaName;
        _loggerFactory  = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);
        _listener       = new LoggingEventListener(_loggerFactory);
        SubscriptionId  = $"test-{Guid.NewGuid():N}";
        Handler         = handler;
        Log             = _loggerFactory.CreateLogger(GetType());
    }

    protected string SubscriptionId { get; }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    protected IntegrationFixture Fixture { get; }

    readonly bool                 _autoStart;
    readonly LoggingEventListener _listener;
    readonly bool                 _subscribeToAll;
    readonly ILoggerFactory       _loggerFactory;

    public virtual async Task InitializeAsync() {
        await Fixture.InitializeAsync();
        var schema           = new Schema(SchemaName);
        var connectionString = Fixture.GetConnectionString();
        await schema.CreateSchema(connectionString, _loggerFactory.CreateLogger<Schema>(), default);

        CheckpointStoreOptions = new SqlServerCheckpointStoreOptions { ConnectionString = connectionString, Schema = SchemaName };
        CheckpointStore        = new SqlServerCheckpointStore(CheckpointStoreOptions);

        var pipe = new ConsumePipe();
        pipe.AddDefaultConsumer(Handler);

        Subscription =
            !_subscribeToAll
                ? new SqlServerStreamSubscription(
                    new SqlServerStreamSubscriptionOptions {
                        ConnectionString = connectionString,
                        Stream           = Stream,
                        SubscriptionId   = SubscriptionId,
                        Schema           = SchemaName
                    },
                    CheckpointStore,
                    pipe,
                    _loggerFactory
                )
                : new SqlServerAllStreamSubscription(
                    new SqlServerAllStreamSubscriptionOptions {
                        ConnectionString = connectionString,
                        SubscriptionId   = SubscriptionId,
                        Schema           = SchemaName
                    },
                    CheckpointStore,
                    pipe,
                    _loggerFactory
                );
        if (_autoStart) await Start();
    }

    public virtual async Task DisposeAsync() {
        if (_autoStart) await Stop();
        await Fixture.DisposeAsync();
        _listener.Dispose();
    }
}
