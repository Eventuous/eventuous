using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Sut.Domain;

public class Booking : Aggregate<BookingState> {
    public void BookRoom(string roomId, StayPeriod period, Money price, string? guestId = null) {
        EnsureDoesntExist();

        Apply(new RoomBooked(roomId, period.CheckIn, period.CheckOut, price.Amount, guestId));
    }

    public void Import(string roomId, StayPeriod period, Money price) {
        EnsureDoesntExist();

        Apply(new BookingImported(roomId, price.Amount, period.CheckIn, period.CheckOut));
    }

    public void Cancel() => Apply(new BookingCancelled());

    public void RecordPayment(string paymentId, Money amount, DateTimeOffset paidAt) {
        EnsureExists();

        if (HasPaymentRecord(paymentId)) return;

        var (previousState, currentState) = Apply(new BookingPaymentRegistered(paymentId, amount.Amount));

        if (previousState.AmountPaid != currentState.AmountPaid) {
            var outstandingAmount = currentState.Price - currentState.AmountPaid;
            Apply(new BookingOutstandingAmountChanged(outstandingAmount.Amount));
            if (outstandingAmount.Amount < 0) Apply(new BookingOverpaid(-outstandingAmount.Amount));
        }

        if (!previousState.IsFullyPaid() && currentState.IsFullyPaid()) Apply(new BookingFullyPaid(paidAt));
    }

    public bool HasPaymentRecord(string paymentId) => Current.OfType<BookingPaymentRegistered>().Any(x => x.PaymentId == paymentId);

    public void Execute() => Apply(new Executed());
}

public record BookingId(string Value) : Id(Value);
