using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Eventuous;
using FluentAssertions;
using static Eventuous.Tests.SqlStreamStore.Events;

namespace Eventuous.Tests.SqlStreamStore
{
    public class StoringEvents: InMemoryFixture // MsSqlFixture
    {
        object[] expectedEvents = {
            new AccountCreated(Guid.NewGuid().ToString()),
            new AmountLodged(100), 
            new AmountWithdrawn(20)
        };

        public StoringEvents() : base(){
            MapEvents();
        }

        [Fact]
        public async Task AppendEventsWithNoStreamVersion_ReadEventsForward()
        {
            string stream = Guid.NewGuid().ToString();
            
            await EventStore.AppendEvents(stream, ExpectedStreamVersion.NoStream, ToStreamEvents(expectedEvents), new CancellationToken());

            var streamEvents = await EventStore.ReadEvents(stream, StreamReadPosition.Start, 3, new CancellationToken());
            var events = ToEvents(streamEvents);

            events.Should().Equal(expectedEvents);
        }

        [Fact]
        public async Task AppendEventsWithNoStreamVersion_ReadEventsBackwards()
        {
            string stream = Guid.NewGuid().ToString();

            await EventStore.AppendEvents(stream, ExpectedStreamVersion.NoStream, ToStreamEvents(expectedEvents), new CancellationToken());

            var streamEvents = await EventStore.ReadEventsBackwards(stream, 3, new CancellationToken());
            var events = ToEvents(streamEvents).Reverse().ToArray();

            events.Should().Equal(expectedEvents);
        }

        [Fact]
        public async Task AppendEventsWithNoStreamVersion_ReadStream()
        {
            string stream = Guid.NewGuid().ToString();

            List<StreamEvent> streamEvents = new List<StreamEvent>();

            await EventStore.AppendEvents(stream, ExpectedStreamVersion.NoStream, ToStreamEvents(expectedEvents), new CancellationToken());

            Action<StreamEvent> eventReceived = (streamEvent) => streamEvents.Add(streamEvent);

            await EventStore.ReadStream(stream, StreamReadPosition.Start, eventReceived, new CancellationToken());

            var events = ToEvents(streamEvents.ToArray());

            events.ToArray().Should().Equal(expectedEvents);
        }

        [Fact]
        public async Task AppendEventsWithAnyStreamVersion_ReadEventsForward()
        {
            string stream = Guid.NewGuid().ToString();

            await EventStore.AppendEvents(stream, ExpectedStreamVersion.Any, ToStreamEvents(expectedEvents), new CancellationToken());

            var streamEvents = await EventStore.ReadEvents(stream, StreamReadPosition.Start, 3, new CancellationToken());
            var events = ToEvents(streamEvents);

            events.Should().Equal(expectedEvents);
        }

        [Fact]
        public async Task AppendEventsMultipleTimes_ReadEventsForward()
        {
            object[] additionalEvents = {
                new AccountCreated(Guid.NewGuid().ToString()),
                new AmountLodged(100), 
                new AmountWithdrawn(20)
            };

            string stream = Guid.NewGuid().ToString();

            await EventStore.AppendEvents(stream, ExpectedStreamVersion.Any, ToStreamEvents(expectedEvents), new CancellationToken());

            await EventStore.AppendEvents(stream, new ExpectedStreamVersion(2), ToStreamEvents(additionalEvents), new CancellationToken());

            var allStreamEvents = await EventStore.ReadEvents(stream, StreamReadPosition.Start, 6, new CancellationToken());
            var allEvents = ToEvents(allStreamEvents);
            var allExpectedEvents = expectedEvents.Concat(additionalEvents);

            allEvents.Should().BeEquivalentTo(allExpectedEvents);
        }

        StreamEvent[] ToStreamEvents(object[] events)
            => events.Select<object, StreamEvent>( @event => new(
                TypeMap.GetTypeName(@event),
                Serializer.Serialize(@event),
                null,
                Serializer.ContentType
            )).ToArray();

        object[] ToEvents(StreamEvent[] streamEvents)
            => streamEvents.Select(e => Serializer.Deserialize(e.Data, e.EventType)).ToArray();

    }
}
