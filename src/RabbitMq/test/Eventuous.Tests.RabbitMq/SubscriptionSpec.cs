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

    static readonly Fixture Auto = new();

    RabbitMqSubscription               _subscription = null!;
    RabbitMqProducer                   _producer     = null!;
    TestEventHandler                   _handler      = null!;
    readonly StreamName                _exchange;
    readonly ILogger<SubscriptionSpec> _log;
    readonly TestEventListener         _es;
    readonly ILoggerFactory            _loggerFactory;
    readonly RabbitMqFixture           _fixture;

    public SubscriptionSpec(RabbitMqFixture fixture) {
        _fixture       = fixture;
        _es            = new();
        _exchange      = new(Auto.Create<string>());
        _loggerFactory = LoggingExtensions.GetLoggerFactory();
        _log           = _loggerFactory.CreateLogger<SubscriptionSpec>();
    }

    [Test]
    public async Task SubscribeAndProduce(CancellationToken cancellationToken) {
        var testEvent = Auto.Create<TestEvent>();
        await _producer.Produce(_exchange, testEvent, new(), cancellationToken: cancellationToken);
        await _handler.AssertThat().Timebox(10.Seconds()).Any().Match(x => x as TestEvent == testEvent).Validate(cancellationToken);
    }

    [Test]
    public async Task SubscribeAndProduceMany(CancellationToken cancellationToken) {
        const int count = 10000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();
        await _producer.Produce(_exchange, testEvents, new(), cancellationToken: cancellationToken);
        await _handler.AssertCollection(30.Seconds(), [..testEvents]).Validate(cancellationToken);
    }

    [Before(Test)]
    public async ValueTask InitializeAsync() {
        _handler  = new();
        _producer = new(_fixture.ConnectionFactory);

        var queue = Auto.Create<string>();

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
    }
}
