using FluentAssertions;
using Xunit;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests {
    public class TypeRegistrationTests {
        readonly TypeMapper _typeMapper;

        public TypeRegistrationTests() => _typeMapper = new TypeMapper();

        [Fact]
        public void ShouldResolveDecoratedEvent() {
            // This test works because event types are registered by the domain Module.cs
            
            _typeMapper.GetTypeName<BookingCancelled>().Should().Be(TypeNames.BookingCancelled);
            _typeMapper.GetType(TypeNames.BookingCancelled).Should().Be<BookingCancelled>();
        }
    }
}