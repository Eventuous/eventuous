using Eventuous.Diagnostics.Logging;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Fixtures;

public abstract class PersistentSubscriptionFixture<T> : IClassFixture<IntegrationFixture>, IAsyncLifetime where T : class, IEventHandler {
    static PersistentSubscriptionFixture()
        => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    protected readonly Fixture Auto = new();

    protected StreamName         Stream       { get; } = new($"test-{Guid.NewGuid():N}");
    protected T                  Handler      { get; }
    protected EventStoreProducer Producer     { get; }
    protected ILogger            Log          { get; }
    StreamPersistentSubscription Subscription { get; }

    protected PersistentSubscriptionFixture(
            IntegrationFixture integrationFixture,
            ITestOutputHelper  outputHelper,
            T                  handler,
            bool               autoStart = true
        ) {
        IntegrationFixture1 = integrationFixture;
        _autoStart          = autoStart;

        var loggerFactory  = TestHelpers.Logging.GetLoggerFactory(outputHelper);
        var subscriptionId = $"test-{Guid.NewGuid():N}";

        Handler  = handler;
        Producer = new EventStoreProducer(integrationFixture.Client);
        Log      = loggerFactory.CreateLogger(GetType());

        _listener = new LoggingEventListener(loggerFactory);

        Subscription = new StreamPersistentSubscription(
            integrationFixture.Client,
            new StreamPersistentSubscriptionOptions {
                StreamName     = Stream,
                SubscriptionId = subscriptionId
            },
            new ConsumePipe().AddDefaultConsumer(Handler),
            loggerFactory
        );
    }

    protected ValueTask Start() => Subscription.SubscribeWithLog(Log);

    protected ValueTask Stop() => Subscription.UnsubscribeWithLog(Log);

    protected IntegrationFixture IntegrationFixture1 { get; }

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
