using Eventuous.Tests.Postgres.Fixtures;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;
using static Eventuous.Tests.Postgres.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Postgres.Store;

public static class Helpers {
    public static StreamName GetStreamName() => new(Instance.Auto.Create<string>());

    public static BookingImported CreateEvent() => ToEvent(DomainFixture.CreateImportBooking());

    public static IEnumerable<BookingImported> CreateEvents(int count) {
        for (var i = 0; i < count; i++) {
            yield return CreateEvent();
        }
    }

    static BookingImported ToEvent(ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    public static Task<AppendEventsResult> AppendEvents(
        StreamName            stream,
        object[]              evt,
        ExpectedStreamVersion version
    ) {
        var streamEvents = evt.Select(x => new StreamEvent(Guid.NewGuid(), x, new Metadata(), "", 0));
        return Instance.EventStore.AppendEvents(stream, version, streamEvents.ToArray(), default);
    }

    public static Task<AppendEventsResult> AppendEvent(StreamName stream, object evt, ExpectedStreamVersion version) {
        var streamEvent = new StreamEvent(Guid.NewGuid(), evt, new Metadata(), "", 0);
        return Instance.EventStore.AppendEvents(stream, version, new[] { streamEvent }, default);
    }
}
