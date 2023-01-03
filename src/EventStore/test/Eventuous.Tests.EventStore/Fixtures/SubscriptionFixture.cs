using Eventuous.Diagnostics.Logging;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Fixtures;

public abstract class SubscriptionFixture<T> : IAsyncLifetime where T : class, IEventHandler {
    static SubscriptionFixture() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected readonly Fixture Auto = new();

    protected StreamName          Stream          { get; } = new($"test-{Guid.NewGuid():N}");
    protected T                   Handler         { get; }
    protected EventStoreProducer  Producer        { get; }
    protected ILogger             Log             { get; }
    protected TestCheckpointStore CheckpointStore { get; }
    StreamSubscription            Subscription    { get; }

    protected SubscriptionFixture(
        ITestOutputHelper    outputHelper,
        T                    handler,
        bool                 autoStart     = true,
        Action<ConsumePipe>? configurePipe = null,
        StreamName?          stream        = null,
        LogLevel             logLevel      = LogLevel.Debug
    ) {
        _autoStart = autoStart;
        if (stream is { } s) Stream = s;

        var loggerFactory  = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);
        var subscriptionId = $"test-{Guid.NewGuid():N}";

        Handler         = handler;
        Producer        = new EventStoreProducer(IntegrationFixture.Instance.Client);
        Log             = loggerFactory.CreateLogger(GetType());
        CheckpointStore = new TestCheckpointStore();

        _listener = new LoggingEventListener(loggerFactory);
        var pipe = new ConsumePipe();
        configurePipe?.Invoke(pipe);
        pipe.AddDefaultConsumer(Handler);

        Subscription = new StreamSubscription(
            IntegrationFixture.Instance.Client,
            new StreamSubscriptionOptions {
                StreamName     = Stream,
                SubscriptionId = subscriptionId,
                ResolveLinkTos = Stream.ToString().StartsWith("$")
            },
            CheckpointStore,
            pipe,
            loggerFactory
        );
    }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    readonly bool                 _autoStart;
    readonly LoggingEventListener _listener;

    public async Task InitializeAsync() {
        if (_autoStart) await Start();
    }

    public async Task DisposeAsync() {
        if (_autoStart) await Stop();
        _listener.Dispose();
    }
}
