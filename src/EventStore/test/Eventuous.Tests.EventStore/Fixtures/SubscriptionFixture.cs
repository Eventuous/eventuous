using Eventuous.Diagnostics.Logging;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Fixtures;

public abstract class SubscriptionFixture<T> : IClassFixture<IntegrationFixture>, IAsyncLifetime where T : class, IEventHandler {
    static SubscriptionFixture() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected readonly Fixture Auto = new();

    protected StreamName          Stream             { get; } = new($"test-{Guid.NewGuid():N}");
    public    IntegrationFixture  IntegrationFixture { get; }
    protected T                   Handler            { get; }
    protected EventStoreProducer  Producer           { get; private set; } = null!;
    protected ILogger             Log                { get; }
    protected TestCheckpointStore CheckpointStore    { get; }
    StreamSubscription            Subscription       { get; set; } = null!;

    protected SubscriptionFixture(
            IntegrationFixture integrationFixture,
            ITestOutputHelper  outputHelper,
            T                  handler,
            bool               autoStart = true,
            StreamName?        stream    = null,
            LogLevel           logLevel  = LogLevel.Debug
        ) {
        _autoStart = autoStart;
        if (stream is { } s) Stream = s;

        LoggerFactory = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);

        IntegrationFixture = integrationFixture;
        Handler            = handler;
        Log                = LoggerFactory.CreateLogger(GetType());
        CheckpointStore    = new TestCheckpointStore();

        _listener = new LoggingEventListener(LoggerFactory);
    }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);
    ILoggerFactory LoggerFactory { get; }

    readonly bool                 _autoStart;
    readonly LoggingEventListener _listener;

    public async Task InitializeAsync() {
        Producer = new EventStoreProducer(IntegrationFixture.Client);

        var subscriptionId = $"test-{Guid.NewGuid():N}";
        var pipe           = new ConsumePipe();
        pipe.AddDefaultConsumer(Handler);

        Subscription = new StreamSubscription(
            IntegrationFixture.Client,
            new StreamSubscriptionOptions {
                StreamName     = Stream,
                SubscriptionId = subscriptionId,
                ResolveLinkTos = Stream.ToString().StartsWith("$")
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
    }
}
