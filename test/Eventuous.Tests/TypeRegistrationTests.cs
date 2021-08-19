using FluentAssertions;
using Xunit;
using static Eventuous.Tests.SutDomain.BookingEvents;

namespace Eventuous.Tests {
    public class TypeRegistrationTests {
        readonly TypeMapper _typeMapper;

        public TypeRegistrationTests() => _typeMapper = new TypeMapper();

        [Fact]
        public void ShouldResolveDecoratedEvent() {
            _typeMapper.RegisterKnownEventTypes();

            _typeMapper.GetTypeName<BookingCancelled>().Should().Be(TypeNames.BookingCancelled);
            _typeMapper.GetType(TypeNames.BookingCancelled).Should().Be<BookingCancelled>();
        }
    }
}