using Eventuous.Tests.Redis.Fixtures;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Redis.Store;

public static class Helpers {
    public static StreamName GetStreamName() => new(Guid.NewGuid().ToString("N"));

    public static BookingImported CreateEvent() => ToEvent(DomainFixture.CreateImportBooking());

    public static IEnumerable<object> CreateEvents(int count) {
        for (var i = 0; i < count; i++) {
            yield return CreateEvent();
        }
    }

    static BookingImported ToEvent(ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    extension(IntegrationFixture fixture) {
        public Task<AppendEventsResult> AppendEvents(
                StreamName            stream,
                object[]              evt,
                ExpectedStreamVersion version,
                CancellationToken     cancellationToken
            ) {
            var streamEvents = evt.Select(x => new NewStreamEvent(Guid.NewGuid(), x, new()));

            return fixture.EventWriter.AppendEvents(stream, version, streamEvents.ToArray(), cancellationToken);
        }

        public Task<AppendEventsResult> AppendEvent(StreamName stream, object evt, ExpectedStreamVersion version, CancellationToken cancellationToken) {
            var streamEvent = new NewStreamEvent(Guid.NewGuid(), evt, new());

            return fixture.EventWriter.AppendEvents(stream, version, [streamEvent], cancellationToken);
        }
    }
}
