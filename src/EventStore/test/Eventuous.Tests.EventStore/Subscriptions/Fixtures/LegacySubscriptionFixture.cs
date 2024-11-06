using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tests.Subscriptions.Base;
using TUnit.Core.Interfaces;
using LoggingExtensions = Eventuous.TestHelpers.TUnit.Logging.LoggingExtensions;

namespace Eventuous.Tests.EventStore.Subscriptions.Fixtures;

public abstract class LegacySubscriptionFixture<T>: IAsyncInitializer, IAsyncDisposable where T : class, IEventHandler {
    protected readonly Fixture Auto = new();

    protected StreamName          Stream          { get; } = new($"test-{Guid.NewGuid():N}");
    protected StoreFixture        StoreFixture    { get; } = new();
    protected T                   Handler         { get; }
    protected EventStoreProducer  Producer        { get; private set; } = null!;
    protected ILogger             Log             { get; }
    protected TestCheckpointStore CheckpointStore { get; }      = new();
    protected StreamSubscription  Subscription    { get; set; } = null!;

    protected LegacySubscriptionFixture(T handler, StreamName? stream = null, LogLevel logLevel = LogLevel.Debug) {
        if (stream is { } s) Stream = s;

        LoggerFactory = LoggingExtensions.GetLoggerFactory(logLevel);
        Handler       = handler;
        Log           = LoggerFactory.CreateLogger(GetType());
        StoreFixture.TypeMapper.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
    }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);
    ILoggerFactory LoggerFactory { get; }

    public async Task InitializeAsync() {
        await StoreFixture.InitializeAsync();
        Producer = new(StoreFixture.Client);

        var subscriptionId = $"test-{Guid.NewGuid():N}";
        var pipe           = new ConsumePipe();
        pipe.AddDefaultConsumer(Handler);

        Subscription = new(
            StoreFixture.Client,
            new() {
                StreamName     = Stream,
                SubscriptionId = subscriptionId,
                ResolveLinkTos = Stream.ToString().StartsWith('$')
            },
            CheckpointStore,
            pipe,
            LoggerFactory
        );
    }

    public async ValueTask DisposeAsync() {
        await StoreFixture.DisposeAsync();
    }
}

public class LegacySubscriptionFixture(TimeSpan? timeout, bool autoStart = true, StreamName? stream = null, LogLevel logLevel = LogLevel.Debug)
    : LegacySubscriptionFixture<TestEventHandler>(new(new(timeout)), stream, logLevel) {
    [Before(Test)]
    public async Task Setup() {
        await InitializeAsync();
        if (autoStart) await Start();
    }

    public async Task Teardown() {
        if (autoStart) await Stop();
        await DisposeAsync();
    }
}
