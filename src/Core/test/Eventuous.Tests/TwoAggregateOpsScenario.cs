using Eventuous.Sut.Domain;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests;

public class TwoAggregateOpsScenario {
    readonly Fixture _fixture = new();

    public TwoAggregateOpsScenario() {
        _testData = _fixture.Create<TestData>();
        var amount = new Money(_testData.Amount);

        _booking.BookRoom(
            _fixture.Create<string>(),
            new StayPeriod(
                LocalDate.FromDateTime(DateTime.Today),
                LocalDate.FromDateTime(DateTime.Today.AddDays(2))
            ),
            amount
        );

        _booking.RecordPayment(_testData.PaymentId, amount, _testData.PaidAt);
    }

    [Fact]
    public void should_produce_fully_paid_event() {
        var expected = new BookingFullyPaid(_testData.PaidAt);
        _booking.Changes.Should().Contain(expected);
    }

    [Fact]
    public void should_produce_payment_registered() {
        var expected = new BookingPaymentRegistered(
            _testData.PaymentId,
            _testData.Amount
        );

        _booking.Changes.Should().Contain(expected);
    }

    [Fact]
    public void should_produce_outstanding_changed() {
        var expected = new BookingOutstandingAmountChanged(0);
        _booking.Changes.Should().Contain(expected);
    }

    [Fact]
    public void should_make_booking_fully_paid()
        => _booking.State.IsFullyPaid().Should().BeTrue();

    [Fact]
    public void should_record_payment()
        => _booking.HasPaymentRecord(_testData.PaymentId).Should().BeTrue();

    [Fact]
    public void should_not_be_overpaid()
        => _booking.State.IsOverpaid().Should().BeFalse();

    readonly Booking  _booking = new();
    readonly TestData _testData;

    record TestData(string PaymentId, float Amount, DateTimeOffset PaidAt);
}
