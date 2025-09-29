using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests;

public class TypeRegistrationTests {
    [Test]
    public async Task ShouldResolveDecoratedEvent() {
        var typeMapper = new TypeMapper();
        typeMapper.RegisterKnownEventTypes(typeof(BookingCancelled).Assembly);
        await Assert.That(typeMapper.GetTypeName<BookingCancelled>()).IsEqualTo(TypeNames.BookingCancelled);
        await Assert.That(typeMapper.GetType(TypeNames.BookingCancelled)).IsEqualTo(typeof(BookingCancelled));
    }

    // Tests for generated mappings
    [Test]
    public async Task ShouldAutoRegisterGeneratedMappings() {
        await Assert.That(TypeMap.Instance.GetTypeName<RoomBooked>()).IsEqualTo(TypeNames.RoomBooked);
        await Assert.That(TypeMap.Instance.GetTypeName<BookingCancelled>()).IsEqualTo(TypeNames.BookingCancelled);
        await Assert.That(TypeMap.Instance.GetTypeName<BookingPaymentRegistered>()).IsEqualTo(TypeNames.PaymentRegistered);
        await Assert.That(TypeMap.Instance.GetTypeName<BookingFullyPaid>()).IsEqualTo(TypeNames.BookingFullyPaid);
        await Assert.That(TypeMap.Instance.GetTypeName<BookingOutstandingAmountChanged>()).IsEqualTo(TypeNames.OutstandingAmountChanged);
        await Assert.That(TypeMap.Instance.GetTypeName<BookingOverpaid>()).IsEqualTo(TypeNames.BookingOverpaid);
        await Assert.That(TypeMap.Instance.GetTypeName<Executed>()).IsEqualTo(TypeNames.Executed);
        await Assert.That(TypeMap.Instance.GetTypeName<BookingImported>()).IsEqualTo(TypeNames.BookingImported);
    }
}