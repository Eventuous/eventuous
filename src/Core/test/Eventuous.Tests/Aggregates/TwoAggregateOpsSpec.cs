using Bogus;
using JetBrains.Annotations;

namespace Eventuous.Tests.Aggregates;

using Sut.Domain;
using Testing;
using static Sut.Domain.BookingEvents;

public class TwoAggregateOpsSpec : AggregateSpec<Booking, BookingState> {
    public TwoAggregateOpsSpec() => _testData = Faker.Generate();

    protected override void When(Booking booking) {
        var amount   = new Money(_testData.Amount);
        var checkIn  = LocalDate.FromDateTime(DateTime.Today);
        var checkOut = checkIn.Plus(Period.FromDays(2));

        booking.BookRoom(Guid.NewGuid().ToString(), new(checkIn, checkOut), amount);
        booking.RecordPayment(_testData.PaymentId, amount, _testData.PaidAt);
    }

    [Test]
    public void should_produce_fully_paid_event() => Emitted(new BookingFullyPaid(_testData.PaidAt));

    [Test]
    public void should_produce_payment_registered() => Emitted(new BookingPaymentRegistered(_testData.PaymentId, _testData.Amount));

    [Test]
    public void should_produce_outstanding_changed() => Emitted(new BookingOutstandingAmountChanged(0));

    [Test]
    public void should_make_booking_fully_paid() => Then().State.IsFullyPaid().Should().BeTrue();

    [Test]
    public void should_record_payment() => Then().HasPaymentRecord(_testData.PaymentId).Should().BeTrue();

    [Test]
    public void should_not_be_overpaid() => Then().State.IsOverpaid().Should().BeFalse();

    readonly TestData _testData;

    [UsedImplicitly]
    record TestData(string PaymentId, float Amount, DateTimeOffset PaidAt);

    static readonly Faker<TestData> Faker = new Faker<TestData>()
        .RuleFor(x => x.PaymentId, f => f.Random.Guid().ToString())
        .RuleFor(x => x.Amount, f => f.Random.Float())
        .RuleFor(x => x.PaidAt, f => f.Date.Past());
}
