using Eventuous.Sut.Domain;

namespace Eventuous.Tests;

using static Fixtures.IdGenerator;

public class StreamNameMapTests {
    readonly StreamNameMap _sut = new();

    [Fact]
    public void Should_get_stream_name_for_id() {
        var idString   = GetId();
        var id         = new BookingId(idString);
        var streamName = StreamNameFactory.For(id);
        streamName.ToString().Should().Be($"Booking-{idString}");
    }

    [Fact]
    public void Should_get_default_stream_name_for_id() {
        var idString   = GetId();
        var id         = new BookingId(idString);
        var streamName = _sut.GetStreamName(id);
        streamName.ToString().Should().Be($"Booking-{idString}");
    }
}
