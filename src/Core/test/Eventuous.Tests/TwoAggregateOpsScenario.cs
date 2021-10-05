using Eventuous.Sut.Domain;

namespace Eventuous.Tests;

public class TwoAggregateOpsScenario {
    readonly Fixture _fixture = new();

    public TwoAggregateOpsScenario() {
        _testData = _fixture.Create<TestData>();

        _booking.BookRoom(
            new BookingId(_testData.Id),
            _fixture.Create<string>(),
            new StayPeriod(
                LocalDate.FromDateTime(DateTime.Today),
                LocalDate.FromDateTime(DateTime.Today.AddDays(2))
            ),
            _testData.Amount
        );

        _booking.RecordPayment(
            _testData.PaymentId,
            _testData.Amount
        );
    }

    [Fact]
    public void should_produce_fully_paid_event() {
        var expected = new BookingEvents.BookingFullyPaid(_testData.Id);
        _booking.Changes.Should().Contain(expected);
    }

    [Fact]
    public void should_produce_payment_registered() {
        var expected = new BookingEvents.BookingPaymentRegistered(
            _testData.Id,
            _testData.PaymentId,
            _testData.Amount
        );

        _booking.Changes.Should().Contain(expected);
    }

    [Fact]
    public void should_make_booking_fully_paid() => _booking.State.IsFullyPaid().Should().BeTrue();

    [Fact]
    public void should_record_payment_in_the_state()
        => _booking.State.HasPaymentRecord(_testData.PaymentId).Should().BeTrue();

    [Fact]
    public void should_not_be_overpaid() => _booking.State.IsOverpaid().Should().BeFalse();

    readonly Booking  _booking = new();
    readonly TestData _testData;

    record TestData(string Id, string PaymentId, decimal Amount);
}
