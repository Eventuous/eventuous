namespace Eventuous.Tests.Aggregates;

using Sut.Domain;
using Testing;

public class OperateOnExistingSpec : AggregateSpec<Booking, BookingState> {
    protected override object[] GivenEvents() => [
        new BookingEvents.RoomBooked("room1", LocalDate.FromDateTime(DateTime.Today), LocalDate.FromDateTime(DateTime.Today.AddDays(2)), 100.0f)
    ];

    protected override void When(Booking booking) => booking.RecordPayment("payment1", new(50), DateTimeOffset.Now);

    [Test]
    public void should_produce_payment_registered() => Emitted(new BookingEvents.BookingPaymentRegistered("payment1", 50));

    [Test]
    public void should_produce_outstanding_changed() => Emitted(new BookingEvents.BookingOutstandingAmountChanged(50));

    [Test]
    public async Task should_not_be_fully_paid() => await Assert.That(Then().State.IsFullyPaid()).IsFalse();

    [Test]
    public async Task should_record_payment() => await Assert.That(Then().HasPaymentRecord("payment1")).IsTrue();

    [Test]
    public async Task should_not_be_overpaid() => await Assert.That(Then().State.IsOverpaid()).IsFalse();

    [Test]
    public async Task should_produce_two_events() => await Assert.That(Then().Changes).HasCount(2);
}
