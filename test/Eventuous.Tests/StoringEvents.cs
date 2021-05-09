using System;
using System.Threading.Tasks;
using AutoFixture;
using Eventuous.Tests.Application;
using Eventuous.Tests.Fixtures;
using Eventuous.Tests.Model;
using FluentAssertions;
using NodaTime;
using Xunit;

namespace Eventuous.Tests {
    public class StoringEvents : NaiveFixture {
        public StoringEvents() {
            Service = new BookingService(AggregateStore);
            BookingEvents.MapBookingEvents();
        }

        BookingService Service { get; }

        [Fact]
        public async Task StoreInitial() {
            var cmd = new Commands.BookRoom(
                Auto.Create<string>(),
                Auto.Create<string>(),
                LocalDate.FromDateTime(DateTime.Today),
                LocalDate.FromDateTime(DateTime.Today.AddDays(2)),
                Auto.Create<decimal>()
            );

            var expected = new object[] {
                new BookingEvents.RoomBooked(
                    cmd.BookingId,
                    cmd.RoomId,
                    cmd.CheckIn,
                    cmd.CheckOut,
                    cmd.Price
                )
            };

            var result = await Service.Handle(cmd, default);

            result.Changes.Should().BeEquivalentTo(expected);
        }
    }
}