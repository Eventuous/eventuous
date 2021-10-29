using System.Diagnostics;
using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Sut.Subs;
using Eventuous.Tests.EventStore.Fixtures;
using Hypothesist;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Eventuous.Tests.EventStore;

public class PubSubTests : IAsyncLifetime {
    static PubSubTests() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    static readonly Fixture Auto = new();

    readonly StreamSubscription _subscription;
    readonly EventStoreProducer _producer;
    readonly TestEventHandler   _handler;

    readonly string           _stream = $"test-{Guid.NewGuid():N}";
    readonly ActivityListener _listener;

    public PubSubTests(ITestOutputHelper outputHelper) {
        var loggerFactory =
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddXunit(outputHelper));

        var subscriptionId = $"test-{Guid.NewGuid():N}";

        _handler = new TestEventHandler();

        _producer = new EventStoreProducer(IntegrationFixture.Instance.Client);

        _subscription = new StreamSubscription(
            IntegrationFixture.Instance.Client,
            new StreamSubscriptionOptions {
                StreamName     = _stream,
                SubscriptionId = subscriptionId
            },
            new NoOpCheckpointStore(),
            new[] { _handler },
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
    }

    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();
        _handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await _producer.Produce(_stream, testEvents);

        await _handler.Validate(10.Seconds());
    }

    public async Task InitializeAsync() {
        await _subscription.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync() {
        await _subscription.StopAsync(CancellationToken.None);
    }
}