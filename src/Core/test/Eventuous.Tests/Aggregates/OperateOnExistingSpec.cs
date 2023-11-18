namespace Eventuous.Tests.Aggregates;

using Sut.Domain;
using Testing;

public class OperateOnExistingSpec : AggregateSpec<Booking> {
    protected override object[] GivenEvents() => [
        new BookingEvents.RoomBooked("room1", LocalDate.FromDateTime(DateTime.Today), LocalDate.FromDateTime(DateTime.Today.AddDays(2)), 100.0f),
    ];

    protected override void When(Booking booking) => booking.RecordPayment("payment1", new Money(50), DateTimeOffset.Now);

    [Fact]
    public void should_produce_payment_registered() {
        Then().Changes.Should().Contain(new BookingEvents.BookingPaymentRegistered("payment1", 50));
    }

    [Fact]
    public void should_produce_outstanding_changed() {
        Then().Changes.Should().Contain(new BookingEvents.BookingOutstandingAmountChanged(50));
    }

    [Fact]
    public void should_not_be_fully_paid() {
        Then().State.IsFullyPaid().Should().BeFalse();
    }

    [Fact]
    public void should_record_payment() {
        Then().HasPaymentRecord("payment1").Should().BeTrue();
    }

    [Fact]
    public void should_not_be_overpaid() {
        Then().State.IsOverpaid().Should().BeFalse();
    }

    [Fact]
    public void should_produce_two_events() {
        Then().Changes.Should().HaveCount(2);
    }
}
