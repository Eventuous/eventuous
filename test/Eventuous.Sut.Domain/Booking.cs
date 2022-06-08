using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Sut.Domain;

public class Booking : Aggregate<BookingState, BookingId> {
    public void BookRoom(BookingId id, string roomId, StayPeriod period, decimal price, string? guestId = null) {
        EnsureDoesntExist();

        Apply(new RoomBooked(id, roomId, period.CheckIn, period.CheckOut, price, guestId));
    }

    public void Import(BookingId id, string roomId, StayPeriod period, decimal price) {
        EnsureDoesntExist();

        Apply(new BookingImported(id, roomId, price, period.CheckIn, period.CheckOut));
    }

    public void RecordPayment(string paymentId, decimal amount) {
        EnsureExists();

        if (State.HasPaymentRecord(paymentId)) return;

        var (previousState, currentState) =
            Apply(new BookingPaymentRegistered(State.Id, paymentId, amount));

        if (previousState.AmountPaid != currentState.AmountPaid) {
            var outstandingAmount = currentState.Price - currentState.AmountPaid;
            Apply(new BookingOutstandingAmountChanged(State.Id, outstandingAmount));
            if (outstandingAmount < 0) Apply(new BookingOverpaid(State.Id, -outstandingAmount));
        }

        if (!previousState.IsFullyPaid() && currentState.IsFullyPaid())
            Apply(new BookingFullyPaid(State.Id));
    }
}

public record BookingId : AggregateId {
    public BookingId(string value) : base(value) { }
}