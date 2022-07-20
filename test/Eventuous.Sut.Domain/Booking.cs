using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Sut.Domain;

public class Booking : Aggregate<BookingState> {
    public void BookRoom(string roomId, StayPeriod period, decimal price, string? guestId = null) {
        EnsureDoesntExist();

        Apply(new RoomBooked(roomId, period.CheckIn, period.CheckOut, price, guestId));
    }

    public void Import(string roomId, StayPeriod period, decimal price) {
        EnsureDoesntExist();

        Apply(new BookingImported(roomId, price, period.CheckIn, period.CheckOut));
    }

    public void RecordPayment(string paymentId, decimal amount, DateTimeOffset paidAt) {
        EnsureExists();

        if (HasPaymentRecord(paymentId)) return;

        var (previousState, currentState) =
            Apply(new BookingPaymentRegistered(paymentId, amount));

        if (previousState.AmountPaid != currentState.AmountPaid) {
            var outstandingAmount = currentState.Price - currentState.AmountPaid;
            Apply(new BookingOutstandingAmountChanged(outstandingAmount));
            if (outstandingAmount < 0) Apply(new BookingOverpaid(-outstandingAmount));
        }

        if (!previousState.IsFullyPaid() && currentState.IsFullyPaid()) Apply(new BookingFullyPaid(paidAt));
    }

    public bool HasPaymentRecord(string paymentId)
        => Current.OfType<BookingPaymentRegistered>().Any(x => x.PaymentId == paymentId);
}

public record BookingId(string Value) : AggregateId(Value);
