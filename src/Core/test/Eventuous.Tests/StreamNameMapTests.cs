using Eventuous.Sut.Domain;

namespace Eventuous.Tests;

using static Fixtures.IdGenerator;

public class StreamNameMapTests {
    readonly StreamNameMap _sut = new();

    [Test]
    public async Task Should_get_stream_name_for_id() {
        var idString   = GetId();
        var id         = new BookingId(idString);
        var streamName = StreamNameFactory.For(id);
        await Assert.That(streamName.ToString()).IsEqualTo($"Booking-{idString}");
    }

    [Test]
    public async Task Should_get_default_stream_name_for_id() {
        var idString   = GetId();
        var id         = new BookingId(idString);
        var streamName = _sut.GetStreamName(id);
        await Assert.That(streamName.ToString()).IsEqualTo($"Booking-{idString}");
    }
}
