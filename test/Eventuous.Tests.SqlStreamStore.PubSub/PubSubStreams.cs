using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using SqlStreamStore;
using Eventuous.Subscriptions;
using Eventuous.Producers.SqlStreamStore;
using Eventuous.Subscriptions.SqlStreamStore;
using Xunit;
using FluentAssertions;
using static Eventuous.Tests.SqlStreamStore.PubSub.Events;

namespace Eventuous.Tests.SqlStreamStore.PubSub
{
    public class PubSubStreams: InMemoryFixture, IDisposable {

        readonly SqlStreamStoreProducer producer; 
        readonly AllStreamSubscription allStreamSubscription;
        readonly StreamSubscription streamSubscription; 
        readonly string stream = "stream";
        readonly string subscription = "subscription";
        readonly MockEventHandler eventHandler; 
        object[] expectedEvents = {
            new AccountCreated(Guid.NewGuid()),
            new AmountLodged(100), 
            new AmountWithdrawn(20)
        };

        public PubSubStreams(): base() {
            producer = new SqlStreamStoreProducer(StreamStore, Serializer);
            eventHandler = new MockEventHandler(subscription);

            streamSubscription = new StreamSubscription(
                StreamStore, 
                stream, 
                subscription, 
                new InMemoryCheckpointStore(), 
                new[] {eventHandler},
                Serializer
            );

            allStreamSubscription = new AllStreamSubscription(
                StreamStore,
                subscription,
                new InMemoryCheckpointStore(),
                new[] {eventHandler},
                Serializer
            );

            MapEvents();
        }

        [Fact]
        public async Task PublishMultipleStreams_SubscribeAllStreams()
        {
            // arrange
            await producer.Initialize();
            await allStreamSubscription.StartAsync(CancellationToken.None);

            // act
            await producer.Produce("stream1", expectedEvents);
            await producer.Produce("stream2", expectedEvents);
            await Task.Delay(5000);
            var events = eventHandler.ReceivedEvents.ToArray();

            // assert
            events.Length.Should().Be(6);
            events.Should().BeEquivalentTo(expectedEvents.Concat(expectedEvents));

            // clean
            await allStreamSubscription.StopAsync(CancellationToken.None);
            await CleanUp();
        }

        [Fact]
        public async Task PublishSingleStream_SubscribeSingleStream()
        {
            // arrange
            await producer.Initialize();
            await streamSubscription.StartAsync(CancellationToken.None);

            // act
            await producer.Produce(stream, expectedEvents);
            await Task.Delay(5000);
            var events = eventHandler.ReceivedEvents.ToArray();

            // assert
            events.Should().Equal(expectedEvents);

            // clean
            await streamSubscription.StopAsync(CancellationToken.None);
            await CleanUp();
        }

        public void Dispose() => CleanUp().Wait();
    }

}