using Eventuous;
using static Bookings.Payments.Domain.PaymentEvents;

namespace Bookings.Payments.Domain;

public record Payment : State<Payment> {
    public string BookingId { get; init; } = null!;
    public float  Amount    { get; init; }

    public Payment() {
        On<PaymentRecorded>(
            (state, recorded) => state with {
                BookingId = recorded.BookingId,
                Amount = recorded.Amount
            }
        );
    }
}

public record PaymentId(string Value) : AggregateId(Value);
