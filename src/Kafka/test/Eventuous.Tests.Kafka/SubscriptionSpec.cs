using Confluent.Kafka;
using Eventuous.Kafka.Producers;
using Eventuous.Kafka.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Filters;
using Eventuous.TestHelpers.TUnit;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.Kafka;

[ClassDataSource<KafkaFixture>]
public class SubscriptionSpec {
    static SubscriptionSpec() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    KafkaBasicProducer              _producer     = null!;
    TestEventHandler                _handler      = null!;
#pragma warning disable TUnit0023
    KafkaBasicSubscription          _subscription = null!;
    TestEventListener               _es           = null!;
#pragma warning restore TUnit0023
    readonly StreamName             _topic;
    readonly ILogger<SubscriptionSpec> _log;
    readonly ILoggerFactory            _loggerFactory;
    readonly KafkaFixture              _fixture;

    public SubscriptionSpec(KafkaFixture fixture) {
        _fixture       = fixture;
        _topic         = new(Guid.NewGuid().ToString());
        _loggerFactory = LoggingExtensions.GetLoggerFactory();
        _log           = _loggerFactory.CreateLogger<SubscriptionSpec>();
    }

    [Test]
    public async Task SubscribeAndProduce(CancellationToken cancellationToken) {
        var testEvent = TestEvent.Create();
        await _producer.Produce(_topic, testEvent, new(), cancellationToken: cancellationToken);
        await _handler.AssertThat().Timebox(10.Seconds()).Any().Match(x => x as TestEvent == testEvent).Validate(cancellationToken);
    }

    [Test]
    public async Task SubscribeAndProduceMany(CancellationToken cancellationToken) {
        const int count = 200;

        var testEvents = TestEvent.CreateMany(count);
        await _producer.Produce(_topic, testEvents, new(), cancellationToken: cancellationToken);
        await _handler.AssertCollection(30.Seconds(), [..testEvents]).Validate(cancellationToken);
    }

    [Before(Test)]
    public async ValueTask InitializeAsync() {
        _es       = new();
        _handler  = new();
        _producer = new(new KafkaProducerOptions(new() { BootstrapServers = _fixture.BootstrapServers }));

        var subscriptionId = Guid.NewGuid().ToString();

        var options = new KafkaSubscriptionOptions {
            ConsumerConfig = new() {
                BootstrapServers     = _fixture.BootstrapServers,
                EnableAutoCommit     = false,
                AutoOffsetReset      = AutoOffsetReset.Earliest,
                EnablePartitionEof   = true
            },
            Topic            = _topic,
            ConcurrencyLimit = 4,
            SubscriptionId   = subscriptionId,
            ThrowOnError     = true
        };

        _subscription = new(
            options,
            new ConsumePipe().AddDefaultConsumer(_handler),
            _loggerFactory
        );

        await _subscription.SubscribeWithLog(_log);
        await _producer.StartAsync(cancellationToken: default);
    }

    [After(Test)]
    public async ValueTask DisposeAsync() {
        await _producer.StopAsync(cancellationToken: default);
        await _subscription.UnsubscribeWithLog(_log);
        _es.Dispose();
        await _subscription.DisposeAsync();
    }
}
