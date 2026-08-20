using Eventuous.Diagnostics.Logging;
using Eventuous.KurrentDB.Producers;
using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using KurrentDB.Client;
using LoggingExtensions = Eventuous.TestHelpers.TUnit.Logging.LoggingExtensions;

namespace Eventuous.Tests.KurrentDB.Subscriptions.Fixtures;

public class PersistentSubscriptionFixture<TSubscription, TOptions, THandler>(
        THandler                                                                  handler,
        Func<string, string, StreamName, THandler, ILoggerFactory, TSubscription> subscriptionFactory,
        bool                                                                      autoStart = true,
        LogLevel                                                                  logLevel  = LogLevel.Information
    )
    where THandler : class, IEventHandler
    where TSubscription : PersistentSubscriptionBase<TOptions>
    where TOptions : PersistentSubscriptionOptions {
    public    StreamName         Stream         { get; }              = new($"test-{Guid.NewGuid():N}");
    public    THandler           Handler        { get; }              = handler;
    public    KurrentDBProducer  Producer       { get; private set; } = null!;
    public    string             SubscriptionId { get; private set; } = null!;
    public    KurrentDBClient    Client         => Fixture.Client;
    protected ILogger            Log            { get; set; }         = null!;
    protected StoreFixture       Fixture        { get; }              = new(logLevel);
    TSubscription                Subscription   { get; set; }         = null!;

    public ValueTask Start() => Subscription.SubscribeWithLog(Log);

    public ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    LoggingEventListener _listener = null!;

    public async ValueTask InitializeAsync() {
        Fixture.TypeMapper.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
        await Fixture.InitializeAsync();
        Producer = new(Fixture.Client);
        var loggerFactory = LoggingExtensions.GetLoggerFactory(logLevel);
        SubscriptionId = $"test-{Guid.NewGuid():N}";
        Log            = loggerFactory.CreateLogger(GetType());

        _listener = new(loggerFactory);

        Subscription = subscriptionFactory(SubscriptionId, Fixture.Container.GetConnectionString(), Stream, Handler, loggerFactory);
        if (autoStart) await Start();
    }

    public async ValueTask DisposeAsync() {
        // Guarded, and the fixture released in the finally: both statements below touch fields that stay null
        // until late in InitializeAsync, so an initialisation that failed earlier than that — a container that
        // never became ready being the realistic case — would otherwise throw past the release.
        try {
            if (autoStart) await Stop();
            _listener.Dispose();
        } catch (Exception) {
            // Whatever went wrong starting up, it must not cost us the container below.
        } finally {
            // The inner fixture owns the container this one started, so it has to go back here: nothing else
            // holds a reference to it, and with it left running every use of this fixture costs the machine
            // another KurrentDB instance until something reaps it.
            await Fixture.DisposeAsync();
        }
    }
}
