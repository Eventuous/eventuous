using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests;

public class TypeRegistrationTests {
    readonly TypeMapper _typeMapper = new();

    public TypeRegistrationTests() => _typeMapper.RegisterKnownEventTypes(typeof(BookingCancelled).Assembly);

    [Test]
    public async Task ShouldResolveDecoratedEvent() {
        await Assert.That(_typeMapper.GetTypeName<BookingCancelled>()).IsEqualTo(TypeNames.BookingCancelled);
        await Assert.That(_typeMapper.GetType(TypeNames.BookingCancelled)).IsEqualTo(typeof(BookingCancelled));
    }
}