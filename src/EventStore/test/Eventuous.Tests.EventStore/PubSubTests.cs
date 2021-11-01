using System.Diagnostics;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Sut.Subs;
using Hypothesist;

namespace Eventuous.Tests.EventStore;

public class PubSubTests : IAsyncLifetime {
    static PubSubTests() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    static readonly Fixture Auto = new();

    readonly StreamSubscription _subscription;
    readonly EventStoreProducer _producer;
    readonly TestEventHandler   _handler;

    readonly StreamName           _stream = new($"test-{Guid.NewGuid():N}");
    readonly ActivityListener     _listener;
    readonly ILogger<PubSubTests> _log;
    readonly TestCheckpointStore  _checkpointStore;

    public PubSubTests(ITestOutputHelper outputHelper) {
        var loggerFactory =
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddXunit(outputHelper));

        var subscriptionId = $"test-{Guid.NewGuid():N}";

        _handler         = new TestEventHandler();
        _producer        = new EventStoreProducer(IntegrationFixture.Instance.Client);
        _log             = loggerFactory.CreateLogger<PubSubTests>();
        _checkpointStore = new TestCheckpointStore();

        _subscription = new StreamSubscription(
            IntegrationFixture.Instance.Client,
            new StreamSubscriptionOptions {
                StreamName     = _stream,
                SubscriptionId = subscriptionId
            },
            _checkpointStore,
            new DefaultConsumer(new IEventHandler[] { _handler }, true),
            loggerFactory
        );

        var log = loggerFactory.CreateLogger("PubSubTest");

        _listener = new ActivityListener {
            ShouldListenTo = _ => true, //_.Name == Instrumentation.Name,
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => log.LogInformation(
                "Started {Activity} with {Id}, parent {ParentId}",
                activity.DisplayName,
                activity.Id,
                activity.ParentId
            ),
            ActivityStopped = activity => log.LogInformation("Stopped {Activity}", activity.DisplayName)
        };

        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public async Task SubscribeAndProduce() {
        var testEvent = Auto.Create<TestEvent>();
        _handler.AssertThat().Any(x => x as TestEvent == testEvent);

        await _producer.Produce(_stream, testEvent);

        await _handler.Validate(10.Seconds());

        _checkpointStore.Last.Position.Should().Be(0);
    }

    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();
        _handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await _producer.Produce(_stream, testEvents);

        await _handler.Validate(10.Seconds());
        
        _checkpointStore.Last.Position.Should().Be(count - 1);
    }

    public async Task InitializeAsync() {
        await _subscription.SubscribeWithLog(_log);
    }

    public async Task DisposeAsync() {
        await _subscription.UnsubscribeWithLog(_log);
    }
}