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

    static readonly Fixture Auto = new();

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
        var testEvent = Auto.Create<TestEvent>();

        await _producer.Produce(_pubsubTopic, testEvent, null, cancellationToken: cancellationToken);

        await _handler.AssertThat().Timebox(10.Seconds()).Any().Match(x => x as TestEvent == testEvent).Validate(cancellationToken);
    }

    [Test]
    [Retry(3)]
    public async Task SubscribeAndProduceMany(CancellationToken cancellationToken) {
        const int count = 10000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

        await _producer.Produce(_pubsubTopic, testEvents, null, cancellationToken: cancellationToken);
        await _handler.AssertCollection(40.Seconds(), [..testEvents]).Validate(cancellationToken);
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
    }
}
