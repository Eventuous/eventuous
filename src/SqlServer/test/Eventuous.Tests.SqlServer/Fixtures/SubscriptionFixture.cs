using Eventuous.Diagnostics.Logging;
using Eventuous.SqlServer;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.SqlServer.Fixtures;

public abstract class SubscriptionFixture<T> : IClassFixture<IntegrationFixture>, IAsyncLifetime where T : class, IEventHandler {
    static SubscriptionFixture() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected StreamName               Stream                 { get; }
    protected T                        Handler                { get; }
    protected ILogger                  Log                    { get; }
    protected SqlServerCheckpointStore CheckpointStore        { get; private set; } = null!;
    SqlServerCheckpointStoreOptions    CheckpointStoreOptions { get; set; }         = null!;
    IMessageSubscription               Subscription           { get; set; }         = null!;
    protected string                   SchemaName             { get; }

    protected SubscriptionFixture(
            IntegrationFixture fixture,
            ITestOutputHelper  outputHelper,
            T                  handler,
            bool               subscribeToAll,
            bool               autoStart = true,
            LogLevel           logLevel  = LogLevel.Debug
        ) {
        _autoStart      = autoStart;
        _fixture        = fixture;
        _subscribeToAll = subscribeToAll;
        Stream          = new StreamName(fixture.Auto.Create<string>());
        SchemaName      = fixture.GetSchemaName();
        _loggerFactory  = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);
        _listener       = new LoggingEventListener(_loggerFactory);
        SubscriptionId  = $"test-{Guid.NewGuid():N}";
        Handler         = handler;
        Log             = _loggerFactory.CreateLogger(GetType());
    }

    protected string SubscriptionId { get; }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    readonly bool                 _autoStart;
    readonly LoggingEventListener _listener;
    readonly IntegrationFixture   _fixture;
    readonly bool                 _subscribeToAll;
    readonly ILoggerFactory       _loggerFactory;

    public virtual async Task InitializeAsync() {
        var schema = new Schema(SchemaName);
        await schema.CreateSchema(_fixture.GetConnection);

        CheckpointStoreOptions = new SqlServerCheckpointStoreOptions { Schema = SchemaName };
        CheckpointStore        = new SqlServerCheckpointStore(_fixture.GetConnection, CheckpointStoreOptions);

        var pipe = new ConsumePipe();
        pipe.AddDefaultConsumer(Handler);

        Subscription =
            !_subscribeToAll
                ? new SqlServerStreamSubscription(
                    _fixture.GetConnection,
                    new SqlServerStreamSubscriptionOptions {
                        Stream         = Stream,
                        SubscriptionId = SubscriptionId,
                        Schema         = SchemaName
                    },
                    CheckpointStore,
                    pipe,
                    _loggerFactory
                )
                : new SqlServerAllStreamSubscription(
                    _fixture.GetConnection,
                    new SqlServerAllStreamSubscriptionOptions {
                        SubscriptionId = SubscriptionId,
                        Schema         = SchemaName
                    },
                    CheckpointStore,
                    pipe,
                    _loggerFactory
                );
        if (_autoStart) await Start();
    }

    public virtual async Task DisposeAsync() {
        if (_autoStart) await Stop();
        _listener.Dispose();
    }
}
