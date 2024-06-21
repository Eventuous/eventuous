using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Persistence.Base.Fixtures;

public static class Helpers {
    public static StreamName GetStreamName(this StoreFixtureBase fixture) => new(fixture.Auto.Create<string>());

    public static BookingImported CreateEvent(this StoreFixtureBase fixture) => ToEvent(DomainFixture.CreateImportBooking(fixture.Auto));

    public static IEnumerable<BookingImported> CreateEvents(this StoreFixtureBase fixture, int count) {
        for (var i = 0; i < count; i++) {
            yield return CreateEvent(fixture);
        }
    }

    static BookingImported ToEvent(ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    public static Task<AppendEventsResult> AppendEvents(
            this StoreFixtureBase fixture,
            StreamName            stream,
            object[]              evt,
            ExpectedStreamVersion version
        ) {
        var streamEvents = evt.Select(x => new StreamEvent(Guid.NewGuid(), x, new Metadata(), "", 0));

        return fixture.EventStore.AppendEvents(stream, version, streamEvents.ToArray(), default);
    }

    public static Task<AppendEventsResult> AppendEvent(
            this StoreFixtureBase fixture,
            StreamName            stream,
            object                evt,
            ExpectedStreamVersion version,
            Metadata?             metadata = null
        ) {
        var streamEvent = new StreamEvent(Guid.NewGuid(), evt, metadata ?? new Metadata(), "", 0);

        return fixture.EventStore.AppendEvents(stream, version, new[] { streamEvent }, default);
    }
}
