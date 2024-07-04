using Eventuous.Diagnostics.Logging;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.EventStore.Subscriptions.Fixtures;

public abstract class PersistentSubscriptionFixture<TSubscription, TOptions, THandler>(
        ITestOutputHelper outputHelper,
        THandler          handler,
        bool              autoStart = true,
        LogLevel          logLevel  = LogLevel.Information
    ) : IAsyncLifetime
    where THandler : class, IEventHandler
    where TSubscription : PersistentSubscriptionBase<TOptions>
    where TOptions : PersistentSubscriptionOptions {

    protected readonly Fixture Auto = new();

    protected StreamName         Stream       { get; }              = new($"test-{Guid.NewGuid():N}");
    protected THandler           Handler      { get; }              = handler;
    protected EventStoreProducer Producer     { get; private set; } = null!;
    protected ILogger            Log          { get; set; }         = null!;
    protected StoreFixture       Fixture      { get; }              = new();
    TSubscription                Subscription { get; set; }         = null!;

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    LoggingEventListener _listener = null!;

    protected abstract TSubscription CreateSubscription(string id, ILoggerFactory loggerFactory);

    public async Task InitializeAsync() {
        Fixture.TypeMapper.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
        await Fixture.InitializeAsync();
        Producer = new(Fixture.Client);
        var loggerFactory  = TestHelpers.Logging.GetLoggerFactory(outputHelper, logLevel);
        var subscriptionId = $"test-{Guid.NewGuid():N}";
        Log = loggerFactory.CreateLogger(GetType());

        _listener = new(loggerFactory);

        Subscription = CreateSubscription(subscriptionId, loggerFactory);
        if (autoStart) await Start();
    }

    public async Task DisposeAsync() {
        if (autoStart) await Stop();
        _listener.Dispose();
    }
}
