using Eventuous;
using static Bookings.Domain.Bookings.BookingEvents;
using static Bookings.Domain.Services;

namespace Bookings.Domain.Bookings;

public class Booking : Aggregate<BookingState> {
    public async Task BookRoom(
        string guestId,
        RoomId roomId,
        StayPeriod period,
        Money price,
        Money prepaid,
        DateTimeOffset bookedAt,
        IsRoomAvailable isRoomAvailable
    ) {
        EnsureDoesntExist();
        await EnsureRoomAvailable(roomId, period, isRoomAvailable);

        var outstanding = price - prepaid;

        Apply(
            new V1.RoomBooked(
                guestId,
                roomId,
                period.CheckIn,
                period.CheckOut,
                price.Amount,
                prepaid.Amount,
                outstanding.Amount,
                price.Currency,
                bookedAt
            )
        );

        MarkFullyPaidIfNecessary(bookedAt);
    }

    public void RecordPayment(
        Money paid,
        string paymentId,
        string paidBy,
        DateTimeOffset paidAt
    ) {
        EnsureExists();

        if (State.HasPaymentBeenRegistered(paymentId)) return;

        var outstanding = State.Outstanding - paid;

        Apply(
            new V1.PaymentRecorded(
                paid.Amount,
                outstanding.Amount,
                paid.Currency,
                paymentId,
                paidBy,
                paidAt
            )
        );

        MarkFullyPaidIfNecessary(paidAt);
        MarkOverpaid(paidAt);
    }

    void MarkFullyPaidIfNecessary(DateTimeOffset when) {
        if (State.Outstanding.Amount <= 0)
            Apply(new V1.BookingFullyPaid(when));
    }

    void MarkOverpaid(DateTimeOffset when) {
        if (State.Outstanding.Amount < 0)
            Apply(new V1.BookingOverpaid(when));
    }

    static async Task EnsureRoomAvailable(RoomId roomId, StayPeriod period, IsRoomAvailable isRoomAvailable) {
        var roomAvailable = await isRoomAvailable(roomId, period);
        if (!roomAvailable) throw new DomainException("Room not available");
    }
}