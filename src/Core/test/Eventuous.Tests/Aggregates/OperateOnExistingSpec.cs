namespace Eventuous.Tests.Aggregates;

using Sut.Domain;
using Testing;

public class OperateOnExistingSpec : AggregateSpec<Booking, BookingState> {
    protected override object[] GivenEvents() => [
        new BookingEvents.RoomBooked("room1", LocalDate.FromDateTime(DateTime.Today), LocalDate.FromDateTime(DateTime.Today.AddDays(2)), 100.0f),
    ];

    protected override void When(Booking booking) => booking.RecordPayment("payment1", new(50), DateTimeOffset.Now);

    [Test]
    public void should_produce_payment_registered() => Emitted(new BookingEvents.BookingPaymentRegistered("payment1", 50));

    [Test]
    public void should_produce_outstanding_changed() => Emitted(new BookingEvents.BookingOutstandingAmountChanged(50));

    [Test]
    public void should_not_be_fully_paid() => Then().State.IsFullyPaid().Should().BeFalse();

    [Test]
    public void should_record_payment() => Then().HasPaymentRecord("payment1").Should().BeTrue();

    [Test]
    public void should_not_be_overpaid() => Then().State.IsOverpaid().Should().BeFalse();

    [Test]
    public void should_produce_two_events() => Then().Changes.Should().HaveCount(2);
}
