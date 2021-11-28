global using NodaTime;
using Eventuous.Tests.Fixtures;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;

namespace Eventuous.Tests;

public class StoringEvents : NaiveFixture {
    public StoringEvents() {
        Service = new BookingService(AggregateStore);
        TypeMap.RegisterKnownEventTypes();
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

        var expected = new Change[] {
            new(
                new BookingEvents.RoomBooked(
                    cmd.BookingId,
                    cmd.RoomId,
                    cmd.CheckIn,
                    cmd.CheckOut,
                    cmd.Price
                ),
                "RoomBooked"
            )
        };

        var result = await Service.Handle(cmd, default);

        result.Changes.Should().BeEquivalentTo(expected);
    }
}