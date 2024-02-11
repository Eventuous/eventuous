using Eventuous.Producers;
using Eventuous.RabbitMq.Producers;
using Eventuous.RabbitMq.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;
using Eventuous.TestHelpers;

namespace Eventuous.Tests.RabbitMq;

public class SubscriptionSpec : IAsyncLifetime, IClassFixture<RabbitMqFixture> {
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

    public SubscriptionSpec(RabbitMqFixture fixture, ITestOutputHelper outputHelper) {
        _fixture  = fixture;
        _es       = new TestEventListener(outputHelper);
        _exchange = new StreamName(Auto.Create<string>());

        _loggerFactory = LoggerFactory.Create(
            builder => builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddXunit(outputHelper, LogLevel.Trace)
        );

        _log = _loggerFactory.CreateLogger<SubscriptionSpec>();
    }

    [Fact]
    public async Task SubscribeAndProduce() {
        var testEvent = Auto.Create<TestEvent>();

        _handler.AssertThat(10.Seconds(), b => b.Any().Match(x => x as TestEvent == testEvent));

        await _producer.Produce(_exchange, testEvent, new Metadata());
        await _handler.Validate();
    }

    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

        _handler.AssertCollection(10.Seconds(), [..testEvents]);

        await _producer.Produce(_exchange, testEvents, new Metadata());
        await _handler.Validate();
    }

    public async Task InitializeAsync() {
        _handler  = new TestEventHandler();
        _producer = new RabbitMqProducer(_fixture.ConnectionFactory);

        var queue = Auto.Create<string>();

        _subscription = new RabbitMqSubscription(
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

    public async Task DisposeAsync() {
        await _producer.StopAsync();
        await _subscription.UnsubscribeWithLog(_log);
        _es.Dispose();
    }
}
