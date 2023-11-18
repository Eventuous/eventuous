namespace Eventuous.Tests.Aggregates;

using Sut.Domain;
using Testing;
using static Sut.Domain.BookingEvents;

public class TwoAggregateOpsSpec : AggregateSpec<Booking> {
    readonly Fixture _fixture = new();

    public TwoAggregateOpsSpec() {
        _testData = _fixture.Create<TestData>();
    }

    protected override void When(Booking booking) {
        var amount = new Money(_testData.Amount);

        booking.BookRoom(
            _fixture.Create<string>(),
            new StayPeriod(LocalDate.FromDateTime(DateTime.Today), LocalDate.FromDateTime(DateTime.Today.AddDays(2))),
            amount
        );

        booking.RecordPayment(_testData.PaymentId, amount, _testData.PaidAt);
    }

    [Fact]
    public void should_produce_fully_paid_event() {
        var expected = new BookingFullyPaid(_testData.PaidAt);
        Then().Changes.Should().Contain(expected);
        ;
    }

    [Fact]
    public void should_produce_payment_registered() {
        var expected = new BookingPaymentRegistered(_testData.PaymentId, _testData.Amount);
        Then().Changes.Should().Contain(expected);
    }

    [Fact]
    public void should_produce_outstanding_changed() {
        var expected = new BookingOutstandingAmountChanged(0);
        Then().Changes.Should().Contain(expected);
    }

    [Fact]
    public void should_make_booking_fully_paid()
        => Then().State.IsFullyPaid().Should().BeTrue();

    [Fact]
    public void should_record_payment()
        => Then().HasPaymentRecord(_testData.PaymentId).Should().BeTrue();

    [Fact]
    public void should_not_be_overpaid()
        => Then().State.IsOverpaid().Should().BeFalse();

    readonly TestData _testData;

    record TestData(string PaymentId, float Amount, DateTimeOffset PaidAt);
}
