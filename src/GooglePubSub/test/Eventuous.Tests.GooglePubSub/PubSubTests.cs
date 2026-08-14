using Eventuous.GooglePubSub.Producers;
using Eventuous.GooglePubSub.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Filters;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Subscriptions.Base;
using Google.Api.Gax;

namespace Eventuous.Tests.GooglePubSub;

[ClassDataSource<PubSubFixture>(Shared = SharedType.PerClass)]
public class PubSubTests {
    static PubSubTests() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    readonly GooglePubSubSubscription _subscription;
    readonly GooglePubSubProducer     _producer;
    readonly TestEventHandler         _handler;
    readonly StreamName               _pubsubTopic;
    readonly string                   _pubsubSubscription;
    readonly ILogger<PubSubTests>     _log;

    // ReSharper disable once UnusedParameter.Local
    public PubSubTests(PubSubFixture _) {
        var loggerFactory = LoggingExtensions.GetLoggerFactory();

        _log                = loggerFactory.CreateLogger<PubSubTests>();
        _pubsubTopic        = new($"test-{Guid.NewGuid():N}");
        _pubsubSubscription = $"test-{Guid.NewGuid():N}";

        _handler = new();

        _producer = new(
            PubSubFixture.PubsubProjectId,
            log: loggerFactory.CreateLogger<GooglePubSubProducer>(),
            configureClient: b => b.EmulatorDetection = EmulatorDetection.EmulatorOnly
        );

        _subscription = new(
            PubSubFixture.PubsubProjectId,
            _pubsubTopic,
            _pubsubSubscription,
            new ConsumePipe().AddDefaultConsumer(_handler),
            loggerFactory,
            configureClient: b => b.EmulatorDetection = EmulatorDetection.EmulatorOnly
        );
    }

    [Test]
    [Retry(3)]
    public async Task SubscribeAndProduce(CancellationToken cancellationToken) {
        var testEvent = TestEvent.Create();

        await _producer.Produce(_pubsubTopic, testEvent, null, cancellationToken: cancellationToken);

        await _handler.AssertThat().Timebox(TimeSpan.FromSeconds(10)).Any().Match(x => x as TestEvent == testEvent).Validate(cancellationToken);
    }

    [Test]
    [Retry(3)]
    public async Task SubscribeAndProduceMany(CancellationToken cancellationToken) {
        const int count = 10000;

        var testEvents = TestEvent.CreateMany(count);

        await _producer.Produce(_pubsubTopic, testEvents, null, cancellationToken: cancellationToken);

        // The expectation watches the whole window, so this is the test's runtime, not just its bound. The
        // emulator delivers all 10k in well under a second; the rest is headroom for a slower machine.
        await _handler.AssertCollection(TimeSpan.FromSeconds(15), [..testEvents]).Validate(cancellationToken);
    }

    [Test]
    [Retry(3)]
    public async Task StopsAndStartsAgain(CancellationToken cancellationToken) {
        // A SubscriberClient can be started and stopped once, so the second run has to build its own. That is
        // why the client is released through the run rather than held on the subscription, and why it is only
        // registered once StartAsync succeeded. Delivery after the restart is what shows the replacement
        // client is the one receiving.
        await _subscription.UnsubscribeWithLog(_log, cancellationToken);
        await _subscription.SubscribeWithLog(_log, cancellationToken);

        var testEvent = TestEvent.Create();

        await _producer.Produce(_pubsubTopic, testEvent, null, cancellationToken: cancellationToken);

        await _handler.AssertThat().Timebox(TimeSpan.FromSeconds(10)).Any().Match(x => x as TestEvent == testEvent).Validate(cancellationToken);
    }

    [Before(Test)]
    public async Task InitializeAsync(CancellationToken cancellationToken) {
        await _producer.StartAsync(cancellationToken);
        await _subscription.SubscribeWithLog(_log, cancellationToken);
    }

    [After(Test)]
    public async Task DisposeAsync(CancellationToken cancellationToken) {
        await _producer.StopAsync(cancellationToken);
        await _subscription.UnsubscribeWithLog(_log, cancellationToken);

        await PubSubFixture.DeleteSubscription(_pubsubSubscription, cancellationToken);
        await PubSubFixture.DeleteTopic(_pubsubTopic, cancellationToken);
        await _subscription.DisposeAsync();
    }
}
