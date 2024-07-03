using Eventuous.Sut.Domain;

namespace Eventuous.Tests;

using static Fixtures.IdGenerator;

public class StreamNameTests {
    [Fact]
    public void Should_get_stream_name_for_state() {
        var idString   = GetId();
        var streamName = StreamName.ForState<BookingState>(idString);
        streamName.ToString().Should().Be($"Booking-{idString}");
    }

    [Fact]
    public void Should_fail_when_id_is_null() {
         Assert.Throws<ArgumentNullException>(() => _ = StreamName.For<BookingState>(null!));
    }
    
    [Fact]
    public void Should_fail_when_id_is_empty() {
         Assert.Throws<ArgumentNullException>(() => _ = StreamName.For<BookingState>("  "));
    }

    [Fact]
    public void Should_trim_id() {
        var streamName = StreamName.ForState<BookingState>("  123");
        streamName.ToString().Should().Be("Booking-123");
    }
}
