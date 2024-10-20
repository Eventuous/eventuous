using Eventuous.Sut.Domain;

namespace Eventuous.Tests;

using static Fixtures.IdGenerator;

public class StreamNameTests {
    [Test]
    public async Task Should_get_stream_name_for_state() {
        var idString   = GetId();
        var streamName = StreamName.ForState<BookingState>(idString);
        await Assert.That(streamName.ToString()).IsEqualTo($"Booking-{idString}");
    }

    [Test]
    public async Task Should_fail_when_id_is_null() {
         await Assert.That(() => _ = StreamName.For<BookingState>(null!)).Throws<ArgumentNullException>();
    }
    
    [Test]
    public async Task Should_fail_when_id_is_empty() {
         await Assert.That(() => _ = StreamName.For<BookingState>("  ")).Throws<ArgumentException>();
    }

    [Test]
    public async Task Should_trim_id() {
        var streamName = StreamName.ForState<BookingState>("  123");
        await Assert.That(streamName.ToString()).IsEqualTo("Booking-123");
    }
}
