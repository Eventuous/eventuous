global using NodaTime;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests;

using Fixtures;
using Sut.App;
using Sut.Domain;

public class StoringEvents : NaiveFixture {
    public StoringEvents() {
        Service = new(EventStore);
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

        Change[] expected = [new(new RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price), TypeNames.RoomBooked)];

        var result = await Service.Handle(cmd, default);

        result.TryGet(out var ok).Should().BeTrue();
        ok!.Changes.Should().BeEquivalentTo(expected);

        var evt = await EventStore.ReadEvents(StreamName.For<Booking>(cmd.BookingId), StreamReadPosition.Start, 1, CancellationToken.None);

        evt[0].Payload.Should().BeEquivalentTo(ok.Changes.First().Event);
    }
}
