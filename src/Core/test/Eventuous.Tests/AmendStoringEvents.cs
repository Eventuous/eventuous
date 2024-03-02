global using NodaTime;

namespace Eventuous.Tests;

using Fixtures;
using Sut.App;
using Sut.Domain;

public class AmendStoringEvents : NaiveFixture {
    public AmendStoringEvents() {
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
            Auto.Create<float>()
        );

        var expected = new Change[] {
            new(
                new BookingEvents.RoomBooked(
                    cmd.RoomId,
                    cmd.CheckIn,
                    cmd.CheckOut,
                    cmd.Price
                ),
                "RoomBooked"
            )
        };

        var scopeData = Guid.NewGuid();

        AmendEvent amend = streamEvent => {
            streamEvent.Metadata.Add("scoped-thing", scopeData);

            return streamEvent;
        };

        var result = await Service.Handle(cmd, amend,  CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Changes.Should().BeEquivalentTo(expected);

        var evt = await EventStore.ReadEvents(
            StreamName.For<Booking>(cmd.BookingId),
            StreamReadPosition.Start,
            1,
            CancellationToken.None
        );

        evt[0].Metadata.Should().ContainKey("scoped-thing").WhoseValue.Should().BeOfType<Guid>().And.Be(scopeData);
    }
}
