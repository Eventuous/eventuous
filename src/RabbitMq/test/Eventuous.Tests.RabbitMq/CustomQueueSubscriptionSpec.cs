using Eventuous.Producers;
using Eventuous.RabbitMq.Producers;
using Eventuous.RabbitMq.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.TestHelpers.TUnit;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.RabbitMq;

[ClassDataSource<RabbitMqFixture>]
public class CustomQueueSubscriptionSpec {
    static CustomQueueSubscriptionSpec() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    RabbitMqProducer                              _producer     = null!;
    TestEventHandler                              _handler      = null!;
#pragma warning disable TUnit0023
    RabbitMqSubscription                          _subscription = null!;
    TestEventListener                             _es           = null!;
#pragma warning restore TUnit0023
    readonly StreamName                           _exchange;
    readonly ILogger<CustomQueueSubscriptionSpec> _log;
    readonly ILoggerFactory                       _loggerFactory;
    readonly RabbitMqFixture                      _fixture;

    public CustomQueueSubscriptionSpec(RabbitMqFixture fixture) {
        _fixture       = fixture;
        _exchange      = new(Guid.NewGuid().ToString());
        _loggerFactory = LoggingExtensions.GetLoggerFactory();
        _log           = _loggerFactory.CreateLogger<CustomQueueSubscriptionSpec>();
    }

    [Test]
    public async Task SubscribeWithCustomQueueName(CancellationToken cancellationToken) {
        var testEvent = TestEvent.Create();
        await _producer.Produce(_exchange, testEvent, new(), cancellationToken: cancellationToken);
        await _handler.AssertThat().Timebox(10.Seconds()).Any().Match(x => x as TestEvent == testEvent).Validate(cancellationToken);
    }

    [Before(Test)]
    public async ValueTask InitializeAsync() {
        _es       = new();
        _handler  = new();
        _producer = new(_fixture.ConnectionFactory);

        var subscriptionId = Guid.NewGuid().ToString();
        var customQueue    = Guid.NewGuid().ToString();

        _subscription = new(
            _fixture.ConnectionFactory,
            new RabbitMqSubscriptionOptions {
                ConcurrencyLimit = 10,
                SubscriptionId   = subscriptionId,
                Exchange         = _exchange,
                ThrowOnError     = true,
                QueueOptions     = new RabbitMqSubscriptionOptions.RabbitMqQueueOptions { Queue = customQueue }
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
