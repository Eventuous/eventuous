using Eventuous.GooglePubSub.Producers;
using Eventuous.GooglePubSub.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;
using Hypothesist;

namespace Eventuous.Tests.GooglePubSub;

public class PubSubTests : IAsyncLifetime {
    static PubSubTests() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    static readonly Fixture Auto = new();

    readonly GooglePubSubSubscription _subscription;
    readonly GooglePubSubProducer     _producer;
    readonly TestEventHandler         _handler;
    readonly StreamName               _pubsubTopic;
    readonly string                   _pubsubSubscription;
    readonly ILogger<PubSubTests>     _log;

    public PubSubTests(ITestOutputHelper outputHelper) {
        var loggerFactory =
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddXunit(outputHelper));

        _log                = loggerFactory.CreateLogger<PubSubTests>();
        _pubsubTopic        = new StreamName($"test-{Guid.NewGuid():N}");
        _pubsubSubscription = $"test-{Guid.NewGuid():N}";

        _handler = new TestEventHandler();

        _producer = new GooglePubSubProducer(PubSubFixture.ProjectId);

        _subscription = new GooglePubSubSubscription(
            PubSubFixture.ProjectId,
            _pubsubTopic,
            _pubsubSubscription,
            new ConsumePipe().AddDefaultConsumer(_handler)
        );
    }

    [Fact]
    public async Task SubscribeAndProduce() {
        var testEvent = Auto.Create<TestEvent>();
        _handler.AssertThat().Any(x => x as TestEvent == testEvent);

        await _producer.Produce(_pubsubTopic, testEvent, null);

        await _handler.Validate(10.Seconds());
    }

    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();
        _handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await _producer.Produce(_pubsubTopic, testEvents, null);

        await _handler.Validate(10.Seconds());
    }

    public async Task InitializeAsync() {
        await _producer.StartAsync();
        await _subscription.SubscribeWithLog(_log);
    }

    public async Task DisposeAsync() {
        await _producer.StopAsync();
        await _subscription.UnsubscribeWithLog(_log);

        await PubSubFixture.DeleteSubscription(_pubsubSubscription);
        await PubSubFixture.DeleteTopic(_pubsubTopic);
    }
}