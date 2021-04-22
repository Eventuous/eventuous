using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eventuous.Producers.RabbitMq;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.RabbitMq;
using FluentAssertions;
using Xunit;

namespace Eventuous.Tests.RabbitMq {
    public class PubSubTests {
        [Fact]
        public async Task SubscribeAndProduce() {
            const string exchange = "test";
            const string queue    = "queue";
            
            TypeMap.AddType<TestEvent>("test-event");

            var serializer = DefaultEventSerializer.Instance;
            var handler    = new Handler();

            var producer = new RabbitMqProducer(RabbitMqFixture.ConnectionFactory, exchange, serializer);

            var subscription = new RabbitMqSubscriptionService(
                RabbitMqFixture.ConnectionFactory,
                queue,
                exchange,
                queue,
                serializer,
                new[] { handler }
            );

            await subscription.StartAsync(CancellationToken.None);

            var testEvent = new TestEvent(Guid.NewGuid().ToString(), int.MaxValue);
            await producer.Produce(testEvent);

            await Task.Delay(50);

            handler.ReceivedEvents.Last().Should().Be(testEvent);
        }

        record TestEvent(string Data, int Number);
        
        class Handler : IEventHandler {
            public string SubscriptionId => "queue";

            public List<object> ReceivedEvents { get; } = new();

            public Task HandleEvent(object evt, long? position) {
                ReceivedEvents.Add(evt);
                return Task.CompletedTask;
            }
        }
    }
}