using Eventuous;

namespace Bookings.Payments.Domain;

using static PaymentEvents;

public record PaymentState : State<PaymentState> {
    public string BookingId { get; init; } = null!;
    public float  Amount    { get; init; }

    public PaymentState() {
        On<PaymentRecorded>((state, recorded) => state with { BookingId = recorded.BookingId, Amount = recorded.Amount });
    }
}