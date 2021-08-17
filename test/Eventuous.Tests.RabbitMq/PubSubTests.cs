using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Eventuous.Producers.RabbitMq;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.RabbitMq;
using FluentAssertions.Extensions;
using Hypothesist;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
        readonly IEventHandler               _handler;
        readonly string                      _exchange;

        public PubSubTests(ITestOutputHelper outputHelper) {
            _exchange = Auto.Create<string>();
            var queue = Auto.Create<string>();

            var loggerFactory =
                LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddXunit(outputHelper));

            _handler = Substitute.For<IEventHandler>();
            _handler.SubscriptionId.Returns(queue);

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
            var hypothesis = Hypothesis
                .For<object>()
                .Any(x => x as TestEvent == testEvent);

            _handler
                .When(x => x.HandleEvent(Arg.Any<object>(), Arg.Any<long>(), Arg.Any<CancellationToken>()))
                .Do(x => hypothesis.Test(x.Arg<object>()));
            
            await _producer.Produce(_exchange, testEvent);
            await hypothesis.Validate(10.Seconds());
        }

        [Fact]
        public async Task SubscribeAndProduceMany() {
            const int count = 10000;

            var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

            var hypothesis = Hypothesis
                .For<object>()
                .Exactly(count, x => testEvents.Contains(x));

            _handler
                .When(x => x.HandleEvent(Arg.Any<object>(), Arg.Any<long>(), Arg.Any<CancellationToken>()))
                .Do(x => hypothesis.Test(x.Arg<object>()));

            await _producer.Produce(_exchange, testEvents);
            await hypothesis.Validate(10.Seconds());
        }

        record TestEvent(string Data, int Number);
        
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