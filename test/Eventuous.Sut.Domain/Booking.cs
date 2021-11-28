using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Sut.Domain;

public class Booking : Aggregate<BookingState, BookingId> {
    public void BookRoom(BookingId id, string roomId, StayPeriod period, decimal price) {
        EnsureDoesntExist();

        Apply(new RoomBooked(id, roomId, period.CheckIn, period.CheckOut, price));
    }

    public void Import(BookingId id, string roomId, StayPeriod period) {
        EnsureDoesntExist();

        Apply(new BookingImported(id, roomId, period.CheckIn, period.CheckOut));
    }

    public void RecordPayment(string paymentId, decimal amount) {
        EnsureExists();

        if (State.HasPaymentRecord(paymentId)) return;

        var (previousState, currentState) =
            Apply(new BookingPaymentRegistered(State.Id, paymentId, amount));

        if (!previousState.IsFullyPaid() && currentState.IsFullyPaid())
            Apply(new BookingFullyPaid(State.Id));
    }
}

public record BookingId(string Value) : AggregateId(Value);