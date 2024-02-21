using Eventuous.Diagnostics.Logging;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Subscriptions;

public abstract class PersistentSubscriptionFixture<T>(
        ITestOutputHelper outputHelper,
        T                 handler,
        bool              autoStart = true,
        LogLevel          logLevel  = LogLevel.Information
    )
    : IAsyncLifetime
    where T : class, IEventHandler {
    static PersistentSubscriptionFixture()
        => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected readonly Fixture Auto = new();

    protected StreamName         Stream       { get; }              = new($"test-{Guid.NewGuid():N}");
    protected T                  Handler      { get; }              = handler;
    protected EventStoreProducer Producer     { get; private set; } = null!;
    protected ILogger            Log          { get; set; }         = null!;
    protected StoreFixture       Fixture      { get; }              = new();
    StreamPersistentSubscription Subscription { get; set; }         = null!;

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    LoggingEventListener       _listener = null!;

    public async Task InitializeAsync() {
        await Fixture.InitializeAsync();
        Producer = new EventStoreProducer(Fixture.Client);
        var loggerFactory  = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);
        var subscriptionId = $"test-{Guid.NewGuid():N}";
        Log = loggerFactory.CreateLogger(GetType());

        _listener = new LoggingEventListener(loggerFactory);

        Subscription = new StreamPersistentSubscription(
            Fixture.Client,
            new StreamPersistentSubscriptionOptions {
                StreamName     = Stream,
                SubscriptionId = subscriptionId
            },
            new ConsumePipe().AddDefaultConsumer(Handler),
            loggerFactory
        );
        if (autoStart) await Start();
    }

    public async Task DisposeAsync() {
        if (autoStart) await Stop();
        _listener.Dispose();
    }
}
