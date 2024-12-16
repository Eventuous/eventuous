using Eventuous.Producers;
using Eventuous.RabbitMq.Producers;
using Eventuous.RabbitMq.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.TestHelpers.TUnit;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.RabbitMq;

[ClassDataSource<RabbitMqFixture>]
public class SubscriptionSpec {
    static SubscriptionSpec() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    RabbitMqSubscription               _subscription = null!;
    RabbitMqProducer                   _producer     = null!;
    TestEventHandler                   _handler      = null!;
    TestEventListener                  _es           = null!;
    readonly StreamName                _exchange;
    readonly ILogger<SubscriptionSpec> _log;
    readonly ILoggerFactory            _loggerFactory;
    readonly RabbitMqFixture           _fixture;

    public SubscriptionSpec(RabbitMqFixture fixture) {
        _fixture       = fixture;
        _exchange      = new(Guid.NewGuid().ToString());
        _loggerFactory = LoggingExtensions.GetLoggerFactory();
        _log           = _loggerFactory.CreateLogger<SubscriptionSpec>();
    }

    [Test]
    public async Task SubscribeAndProduce(CancellationToken cancellationToken) {
        var testEvent = TestEvent.Create();
        await _producer.Produce(_exchange, testEvent, new(), cancellationToken: cancellationToken);
        await _handler.AssertThat().Timebox(10.Seconds()).Any().Match(x => x as TestEvent == testEvent).Validate(cancellationToken);
    }

    [Test]
    public async Task SubscribeAndProduceMany(CancellationToken cancellationToken) {
        const int count = 10000;

        var testEvents = TestEvent.CreateMany(count);
        await _producer.Produce(_exchange, testEvents, new(), cancellationToken: cancellationToken);
        await _handler.AssertCollection(30.Seconds(), [..testEvents]).Validate(cancellationToken);
    }

    [Before(Test)]
    public async ValueTask InitializeAsync() {
        _es       = new();
        _handler  = new();
        _producer = new(_fixture.ConnectionFactory);

        var queue = Guid.NewGuid().ToString();

        _subscription = new(
            _fixture.ConnectionFactory,
            new RabbitMqSubscriptionOptions {
                ConcurrencyLimit = 10,
                SubscriptionId   = queue,
                Exchange         = _exchange,
                ThrowOnError     = true
            },
            new ConsumePipe().AddDefaultConsumer(_handler),
            _loggerFactory
        );
        await _subscription.SubscribeWithLog(_log);
        await _producer.StartAsync();
    }

    [After(Test)]
    public async ValueTask DisposeAsync() {
        await _producer.StopAsync();
        await _subscription.UnsubscribeWithLog(_log);
        _es.Dispose();
        await _subscription.DisposeAsync();
    }
}
