using System;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using Eventuous.Tests.Application;
using Eventuous.Tests.Fixtures;
using Eventuous.Tests.Model;
using FluentAssertions;
using NodaTime;
using Xunit;

namespace Eventuous.Tests {
    public class AppServiceTests {
        static readonly IntegrationFixture Fixture = new();

        public AppServiceTests() {
            Service = new BookingService(Fixture.AggregateStore);
            BookingEvents.MapBookingEvents();
        }

        BookingService Service { get; }

        [Fact]
        public async Task ProcessAnyForNew() {
            var cmd = new Commands.ImportBooking(
                Fixture.Auto.Create<string>(),
                Fixture.Auto.Create<string>(),
                LocalDate.FromDateTime(DateTime.Today),
                LocalDate.FromDateTime(DateTime.Today.AddDays(2))
            );

            var expected = new object[] {
                new BookingEvents.BookingImported(
                    cmd.BookingId,
                    cmd.RoomId,
                    cmd.CheckIn,
                    cmd.CheckOut
                )
            };

            await Service.Handle(cmd, default);

            var events = await Fixture.EventStore.ReadEvents(
                StreamName.For<Booking>(cmd.BookingId),
                StreamReadPosition.Start,
                int.MaxValue,
                default
            );

            var result = events.Select(x => Fixture.Serializer.Deserialize(x.Data, x.EventType)).ToArray();

            result.Should().BeEquivalentTo(expected);
        }
    }
}