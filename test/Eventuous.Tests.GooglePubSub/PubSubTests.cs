using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Eventuous.Producers.GooglePubSub;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.GooglePubSub;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Eventuous.Tests.GooglePubSub {
    public class PubSubTests : IAsyncLifetime {
        static PubSubTests() => TypeMap.AddType<TestEvent>("test-event");

        static readonly Fixture          Auto       = new();
        static readonly IEventSerializer Serializer = DefaultEventSerializer.Instance;

        readonly GooglePubSubSubscription _subscription;
        readonly GooglePubSubProducer     _producer;
        readonly Handler                  _handler;
        readonly string                   _pubsubTopic;
        readonly string                   _pubsubSubscription;

        public PubSubTests(ITestOutputHelper outputHelper) {
            var loggerFactory =
                LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddXunit(outputHelper));

            _pubsubTopic        = $"test-{Guid.NewGuid():N}";
            _pubsubSubscription = $"test-{Guid.NewGuid():N}";

            _handler = new Handler(_pubsubSubscription);

            _producer = new GooglePubSubProducer(
                PubSubFixture.ProjectId,
                _pubsubTopic,
                Serializer,
                loggerFactory: loggerFactory
            );

            _subscription = new GooglePubSubSubscription(
                PubSubFixture.ProjectId,
                _pubsubTopic,
                _pubsubSubscription,
                Serializer,
                new[] { _handler },
                loggerFactory: loggerFactory
            );
        }

        [Fact]
        public async Task SubscribeAndProduce() {
            var testEvent = Auto.Create<TestEvent>();
            await _producer.Produce(_pubsubTopic, testEvent);

            await Task.Delay(2000);

            _handler.ReceivedEvents.Last().Should().Be(testEvent);
        }

        [Fact]
        public async Task SubscribeAndProduceMany() {
            const int count = 10000;

            var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

            await _producer.Produce(_pubsubTopic, testEvents);

            await Task.Delay(count / 2);

            _handler.ReceivedEvents.Count.Should().Be(testEvents.Count);

            while (_handler.ReceivedEvents.TryTake(out var re)) {
                testEvents.Should().Contain(re as TestEvent);
            }
        }

        class Handler : IEventHandler {
            public Handler(string subscriptionId) => SubscriptionId = subscriptionId;

            public string SubscriptionId { get; }

            public ConcurrentBag<object> ReceivedEvents { get; } = new();

            public Task HandleEvent(object evt, long? position, CancellationToken cancellationToken) {
                ReceivedEvents.Add(evt);
                return Task.CompletedTask;
            }
        }

        public async Task InitializeAsync() {
            await _producer.Initialize();
            await _subscription.StartAsync(CancellationToken.None);
        }

        public async Task DisposeAsync() {
            await _producer.Shutdown();
            await _subscription.StopAsync(CancellationToken.None);
            await PubSubFixture.DeleteSubscription(_pubsubSubscription);
            await PubSubFixture.DeleteTopic(_pubsubTopic);
        }
    }

    record TestEvent(string Data, int Number);
}