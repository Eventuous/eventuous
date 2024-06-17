using Eventuous;
using static Bookings.Payments.Domain.PaymentEvents;

namespace Bookings.Payments.Domain;

public class Payment : Aggregate<PaymentState> {
    public void ProcessPayment(string bookingId, Money amount, string method, string provider)
        => Apply(new PaymentRecorded(bookingId, amount.Amount, amount.Currency, method, provider));
}

public record PaymentState : State<PaymentState> {
    public string BookingId { get; init; } = null!;
    public float  Amount    { get; init; }

    public PaymentState() {
        On<PaymentRecorded>(
            (state, recorded) => state with {
                BookingId = recorded.BookingId,
                Amount = recorded.Amount
            }
        );
    }
}

public record PaymentId(string Value) : Id(Value);
