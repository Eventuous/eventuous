using FluentAssertions;
using Xunit;
using static Eventuous.Tests.Model.BookingEvents;

namespace Eventuous.Tests {
    public class TypeRegistrationTests {
        [Fact]
        public void ShouldResolveDecoratedEvent() {
            TypeMap.RegisterKnownEventTypes(typeof(RoomBooked).Assembly);

            TypeMap.GetTypeName<BookingCancelled>().Should().Be(BookingCancelledTypeName);
            TypeMap.GetType(BookingCancelledTypeName).Should().Be<BookingCancelled>();
        }
    }
}