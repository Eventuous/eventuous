using Eventuous.Tests.EventStore.Fixtures;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using static Eventuous.Tests.EventStore.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.EventStore;

public class AppServiceTests {
    static AppServiceTests() => BookingEvents.MapBookingEvents();

    static BookingService Service { get; } = new(Instance.AggregateStore);

    [Fact]
    public async Task ProcessAnyForNew() {
        var cmd = DomainFixture.CreateImportBooking();

        var expected = new object[] {
            new BookingEvents.BookingImported(
                cmd.BookingId,
                cmd.RoomId,
                cmd.CheckIn,
                cmd.CheckOut
            )
        };

        await Service.Handle(cmd, default);

        var events = await Instance.EventStore.ReadEvents(
            StreamName.For<Booking>(cmd.BookingId),
            StreamReadPosition.Start,
            int.MaxValue,
            default
        );

        var result = events.Select(x => Serializer.Json.DeserializeEvent(x.Data, x.EventType)).ToArray();

        result.Should().BeEquivalentTo(expected);
    }
}