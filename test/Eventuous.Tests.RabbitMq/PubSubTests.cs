using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Eventuous.RabbitMq.Producers;
using Eventuous.RabbitMq.Subscriptions;
using Eventuous.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Eventuous.Tests.RabbitMq {
    public class PubSubTests : IAsyncLifetime {
        static PubSubTests() {
            TypeMap.AddType<TestEvent>("test-event");
        }

        static readonly Fixture Auto = new();

        readonly RabbitMqSubscriptionService _subscription;
        readonly RabbitMqProducer            _producer;
        readonly Handler                     _handler;
        readonly string                      _exchange;

        public PubSubTests(ITestOutputHelper outputHelper) {
            _exchange = Auto.Create<string>();
            var queue = Auto.Create<string>();

            var loggerFactory =
                LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddXunit(outputHelper));

            _handler = new Handler();

            _producer = new RabbitMqProducer(RabbitMqFixture.ConnectionFactory);

            _subscription = new RabbitMqSubscriptionService(
                RabbitMqFixture.ConnectionFactory,
                new RabbitMqSubscriptionOptions {
                    ConcurrencyLimit  = 10,
                    SubscriptionQueue = queue,
                    Exchange          = _exchange,
                    SubscriptionId    = queue
                },
                new[] { _handler },
                loggerFactory: loggerFactory
            );
        }

        [Fact]
        public async Task SubscribeAndProduce() {
            var testEvent = Auto.Create<TestEvent>();
            await _producer.Produce(_exchange, testEvent);

            await Task.Delay(50);

            _handler.ReceivedEvents.Last().Should().Be(testEvent);
        }

        [Fact]
        public async Task SubscribeAndProduceMany() {
            const int count = 10000;

            var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

            await _producer.Produce(_exchange, testEvents);

            await Task.Delay(count / 2);

            _handler.ReceivedEvents.Count.Should().Be(testEvents.Count);

            while (_handler.ReceivedEvents.TryTake(out var re)) {
                testEvents.Should().Contain(re as TestEvent);
            }
        }

        record TestEvent(string Data, int Number);

        class Handler : IEventHandler {
            public string SubscriptionId => "queue";

            public ConcurrentBag<object> ReceivedEvents { get; } = new();

            public Task HandleEvent(object evt, long? position, CancellationToken cancellationToken) {
                ReceivedEvents.Add(evt);
                return Task.CompletedTask;
            }
        }

        public async Task InitializeAsync() {
            await _subscription.StartAsync(CancellationToken.None);
            await _producer.Initialize();
        }

        public async Task DisposeAsync() {
            await _producer.Shutdown();
            await _subscription.StopAsync(CancellationToken.None);
        }
    }
}