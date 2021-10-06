using System.Linq;
using System.Threading;
using Eventuous.Producers;
using Eventuous.RabbitMq.Producers;
using Eventuous.RabbitMq.Subscriptions;
using Eventuous.Sut.Subs;
using FluentAssertions.Extensions;
using Hypothesist;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Eventuous.Tests.RabbitMq;

public class SubscriptionSpec : IAsyncLifetime {
    static SubscriptionSpec() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    static readonly Fixture Auto = new();

    readonly RabbitMqSubscriptionService _subscription;
    readonly RabbitMqProducer            _producer;
    readonly TestEventHandler            _handler;
    readonly string                      _exchange;

    public SubscriptionSpec(ITestOutputHelper outputHelper) {
        _exchange = Auto.Create<string>();
        var queue = Auto.Create<string>();

        var loggerFactory =
            LoggerFactory.Create(
                builder => builder
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddXunit(outputHelper, LogLevel.Trace)
            );

        _handler = new TestEventHandler(queue);

        _producer = new RabbitMqProducer(RabbitMqFixture.ConnectionFactory);

        _subscription = new RabbitMqSubscriptionService(
            RabbitMqFixture.ConnectionFactory,
            new RabbitMqSubscriptionOptions {
                ConcurrencyLimit = 10,
                SubscriptionId   = queue,
                Exchange         = _exchange,
                ThrowOnError     = true
            },
            new[] { _handler },
            loggerFactory
        );
    }

    [Fact]
    public async Task SubscribeAndProduce() {
        var testEvent = Auto.Create<TestEvent>();

        _handler.AssertThat().Any(x => x as TestEvent == testEvent);

        await _producer.Produce(_exchange, testEvent);
        await _handler.Validate(10.Seconds());
    }

    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

        _handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await _producer.Produce(_exchange, testEvents);
        await _handler.Validate(10.Seconds());
    }

    public async Task InitializeAsync() {
        await _subscription.StartAsync(CancellationToken.None);
        await _producer.StartAsync();
    }

    public async Task DisposeAsync() {
        await _producer.StopAsync();
        await _subscription.StopAsync(CancellationToken.None);
    }
}