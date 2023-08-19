using Eventuous.Tests.Redis.Fixtures;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Redis.Store;

public static class Helpers {
    public static StreamName GetStreamName()
        => new(SharedAutoFixture.Auto.Create<string>());

    public static BookingImported CreateEvent()
        => ToEvent(DomainFixture.CreateImportBooking());

    public static IEnumerable<object> CreateEvents(int count) {
        for (var i = 0; i < count; i++) {
            yield return CreateEvent();
        }
    }

    static BookingImported ToEvent(ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    public static Task<AppendEventsResult> AppendEvents(
            this IntegrationFixture fixture,
            StreamName              stream,
            object[]                evt,
            ExpectedStreamVersion   version
        ) {
        var streamEvents = evt.Select(x => new StreamEvent(Guid.NewGuid(), x, new Metadata(), "", 0));

        return fixture.EventWriter.AppendEvents(stream, version, streamEvents.ToArray(), default);
    }

    public static Task<AppendEventsResult> AppendEvent(this IntegrationFixture fixture, StreamName stream, object evt, ExpectedStreamVersion version) {
        var streamEvent = new StreamEvent(Guid.NewGuid(), evt, new Metadata(), "", 0);

        return fixture.EventWriter.AppendEvents(stream, version, new[] { streamEvent }, default);
    }
}
