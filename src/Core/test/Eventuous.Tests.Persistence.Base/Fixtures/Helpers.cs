using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Persistence.Base.Fixtures;

public static class Helpers {
    public static StreamName GetStreamName() => new(Guid.NewGuid().ToString());

    public static BookingImported CreateEvent() => ToEvent(DomainFixture.CreateImportBooking());

    public static IEnumerable<BookingImported> CreateEvents(this StoreFixtureBase fixture, int count) {
        for (var i = 0; i < count; i++) {
            yield return CreateEvent();
        }
    }

    static BookingImported ToEvent(ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    public static Task<AppendEventsResult> AppendEvents(
            this StoreFixtureBase fixture,
            StreamName            stream,
            object[]              evt,
            ExpectedStreamVersion version
        ) {
        var streamEvents = evt.Select(x => new NewStreamEvent(Guid.NewGuid(), x, new()));

        return fixture.EventStore.AppendEvents(stream, version, streamEvents.ToArray(), default);
    }

    public static Task<AppendEventsResult> AppendEvent(
            this StoreFixtureBase fixture,
            StreamName            stream,
            object                evt,
            ExpectedStreamVersion version,
            Metadata?             metadata = null
        ) {
        var streamEvent = new NewStreamEvent(Guid.NewGuid(), evt, metadata ?? new Metadata());

        return fixture.EventStore.AppendEvents(stream, version, [streamEvent], default);
    }
    

    public static Task<AppendEventsResult> StoreChanges(
            this StoreFixtureBase fixture,
            StreamName            stream,
            object                evt,
            ExpectedStreamVersion version
        ) {
        return fixture.EventStore.Store(stream, version, [evt]);
    }
}
