using System.Linq;
using System.Threading;
using Eventuous.GooglePubSub.Producers;
using Eventuous.GooglePubSub.Subscriptions;
using Eventuous.Producers;
using Eventuous.Sut.Subs;
using FluentAssertions.Extensions;
using Hypothesist;

namespace Eventuous.Tests.GooglePubSub {
    public class PubSubTests : IAsyncLifetime {
        static PubSubTests() => TypeMap.Instance.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

        static readonly Fixture Auto = new();

        readonly GooglePubSubSubscription _subscription;
        readonly GooglePubSubProducer     _producer;
        readonly TestEventHandler         _handler;
        readonly string                   _pubsubTopic;
        readonly string                   _pubsubSubscription;

        public PubSubTests(ITestOutputHelper outputHelper) {
            var loggerFactory =
                LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddXunit(outputHelper));

            _pubsubTopic        = $"test-{Guid.NewGuid():N}";
            _pubsubSubscription = $"test-{Guid.NewGuid():N}";

            _handler = new TestEventHandler();

            _producer = new GooglePubSubProducer(
                PubSubFixture.ProjectId,
                loggerFactory: loggerFactory
            );

            _subscription = new GooglePubSubSubscription(
                PubSubFixture.ProjectId,
                _pubsubTopic,
                _pubsubSubscription,
                new[] { _handler },
                loggerFactory: loggerFactory
            );
        }

        [Fact]
        public async Task SubscribeAndProduce() {
            var testEvent = Auto.Create<TestEvent>();
            _handler.AssertThat().Any(x => x as TestEvent == testEvent);

            await _producer.Produce(_pubsubTopic, testEvent);
            
            await _handler.Validate(10.Seconds());
        }

        [Fact]
        public async Task SubscribeAndProduceMany() {
            const int count = 10000;

            var testEvents = Auto.CreateMany<TestEvent>(count).ToList();
            _handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

            await _producer.Produce(_pubsubTopic, testEvents);

            await _handler.Validate(10.Seconds());
        }

        public async Task InitializeAsync() {
            await _producer.StartAsync();
            await _subscription.StartAsync(CancellationToken.None);
        }

        public async Task DisposeAsync() {
            await _producer.StopAsync();
            await _subscription.StopAsync(CancellationToken.None);
            await PubSubFixture.DeleteSubscription(_pubsubSubscription);
            await PubSubFixture.DeleteTopic(_pubsubTopic);
        }
    }
}