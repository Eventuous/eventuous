using Eventuous.Producers;
using Eventuous.RabbitMq.Producers;
using Eventuous.RabbitMq.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;
using Eventuous.TestHelpers;
using Hypothesist;

namespace Eventuous.Tests.RabbitMq;

public class SubscriptionSpec : IAsyncLifetime, IDisposable {
    static SubscriptionSpec() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    static readonly Fixture Auto = new();
    
    readonly RabbitMqSubscription      _subscription;
    readonly RabbitMqProducer          _producer;
    readonly TestEventHandler          _handler;
    readonly StreamName                _exchange;
    readonly ILogger<SubscriptionSpec> _log;
    readonly TestEventListener         _es;

    public SubscriptionSpec(ITestOutputHelper outputHelper) {
        _es = new TestEventListener(outputHelper);

        _exchange = new StreamName(Auto.Create<string>());
        var queue = Auto.Create<string>();

        var loggerFactory =
            LoggerFactory.Create(
                builder => builder
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddXunit(outputHelper, LogLevel.Trace)
            );

        _log      = loggerFactory.CreateLogger<SubscriptionSpec>();
        _handler  = new TestEventHandler();
        _producer = new RabbitMqProducer(RabbitMqFixture.ConnectionFactory);

        _subscription = new RabbitMqSubscription(
            RabbitMqFixture.ConnectionFactory,
            new RabbitMqSubscriptionOptions {
                ConcurrencyLimit = 10,
                SubscriptionId   = queue,
                Exchange         = _exchange,
                ThrowOnError     = true
            },
            new ConsumePipe().AddDefaultConsumer(_handler)
        );
    }

    [Fact]
    public async Task SubscribeAndProduce() {
        var testEvent = Auto.Create<TestEvent>();

        _handler.AssertThat().Any(x => x as TestEvent == testEvent);

        await _producer.Produce(_exchange, testEvent, new Metadata());
        await _handler.Validate(10.Seconds());
    }

    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

        _handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await _producer.Produce(_exchange, testEvents, new Metadata());
        await _handler.Validate(10.Seconds());
    }

    public async Task InitializeAsync() {
        await _subscription.SubscribeWithLog(_log);
        await _producer.StartAsync();
    }

    public async Task DisposeAsync() {
        await _producer.StopAsync();
        await _subscription.UnsubscribeWithLog(_log);
    }

    public void Dispose() => _es.Dispose();
}