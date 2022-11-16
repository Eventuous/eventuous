using Eventuous.TypeMap;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests;

public class TypeRegistrationTests {
    readonly TypeMapper _typeMapper;

    public TypeRegistrationTests() {
        _typeMapper = new TypeMapper();
        _typeMapper.RegisterKnownEventTypes(typeof(BookingCancelled).Assembly);
    }

    [Fact]
    public void ShouldResolveDecoratedEvent() {
        _typeMapper.GetTypeName<BookingCancelled>().Should().Be(TypeNames.BookingCancelled);
        _typeMapper.GetType(TypeNames.BookingCancelled).Should().Be<BookingCancelled>();
    }
}