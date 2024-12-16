using Eventuous.Diagnostics.Logging;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using LoggingExtensions = Eventuous.TestHelpers.TUnit.Logging.LoggingExtensions;

namespace Eventuous.Tests.EventStore.Subscriptions.Fixtures;

public class PersistentSubscriptionFixture<TSubscription, TOptions, THandler>(
        THandler                                                                  handler,
        Func<string, string, StreamName, THandler, ILoggerFactory, TSubscription> subscriptionFactory,
        bool                                                                      autoStart = true,
        LogLevel                                                                  logLevel  = LogLevel.Information
    )
    where THandler : class, IEventHandler
    where TSubscription : PersistentSubscriptionBase<TOptions>
    where TOptions : PersistentSubscriptionOptions {
    public    StreamName         Stream       { get; }              = new($"test-{Guid.NewGuid():N}");
    public    THandler           Handler      { get; }              = handler;
    public    EventStoreProducer Producer     { get; private set; } = null!;
    protected ILogger            Log          { get; set; }         = null!;
    protected StoreFixture       Fixture      { get; }              = new();
    TSubscription                Subscription { get; set; }         = null!;

    public ValueTask Start() => Subscription.SubscribeWithLog(Log);

    public ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    LoggingEventListener _listener = null!;

    public async ValueTask InitializeAsync() {
        Fixture.TypeMapper.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
        await Fixture.InitializeAsync();
        Producer = new(Fixture.Client);
        var loggerFactory  = LoggingExtensions.GetLoggerFactory(logLevel);
        var subscriptionId = $"test-{Guid.NewGuid():N}";
        Log = loggerFactory.CreateLogger(GetType());

        _listener = new(loggerFactory);

        Subscription = subscriptionFactory(subscriptionId, Fixture.Container.GetConnectionString(), Stream, Handler, loggerFactory);
        if (autoStart) await Start();
    }

    public async ValueTask DisposeAsync() {
        if (autoStart) await Stop();
        _listener.Dispose();
    }
}
