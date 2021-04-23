using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Eventuous.Producers.RabbitMq;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.RabbitMq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Eventuous.Tests.RabbitMq {
    public class PubSubTests : IAsyncLifetime {
        static PubSubTests() {
            TypeMap.AddType<TestEvent>("test-event");
        }

        static readonly Fixture          Auto       = new();
        static readonly IEventSerializer Serializer = DefaultEventSerializer.Instance;

        readonly RabbitMqSubscriptionService _subscription;
        readonly RabbitMqProducer            _producer;
        readonly Handler                     _handler;

        public PubSubTests(ITestOutputHelper outputHelper) {
            var exchange = Auto.Create<string>();
            var queue    = Auto.Create<string>();

            var loggerFactory =
                LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddXunit(outputHelper));

            _handler = new Handler();

            _producer = new RabbitMqProducer(RabbitMqFixture.ConnectionFactory, exchange, Serializer);

            _subscription = new RabbitMqSubscriptionService(
                RabbitMqFixture.ConnectionFactory,
                queue,
                exchange,
                "queue",
                Serializer,
                new[] { _handler },
                10,
                loggerFactory
            );
        }

        [Fact]
        public async Task SubscribeAndProduce() {
            var testEvent = Auto.Create<TestEvent>();
            await _producer.Produce(testEvent);

            await Task.Delay(50);

            _handler.ReceivedEvents.Last().Should().Be(testEvent);
        }

        [Fact]
        public async Task SubscribeAndProduceMany() {
            const int count = 10000;

            var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

            await _producer.Produce(testEvents);

            await Task.Delay(count / 5);

            _handler.ReceivedEvents.Count.Should().Be(testEvents.Count);

            while (_handler.ReceivedEvents.TryTake(out var re)) {
                testEvents.Should().Contain(re as TestEvent);
            }
        }

        record TestEvent(string Data, int Number);

        class Handler : IEventHandler {
            public string SubscriptionId => "queue";

            public ConcurrentBag<object> ReceivedEvents { get; } = new();

            public Task HandleEvent(object evt, long? position) {
                ReceivedEvents.Add(evt);
                return Task.CompletedTask;
            }
        }

        public Task InitializeAsync() => _subscription.StartAsync(CancellationToken.None);

        public Task DisposeAsync() => _subscription.StopAsync(CancellationToken.None);
    }
}