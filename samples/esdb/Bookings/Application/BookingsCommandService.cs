using Bookings.Domain;
using Bookings.Domain.Bookings;
using Eventuous;
using NodaTime;
using static Bookings.Application.BookingCommands;

namespace Bookings.Application;

public class BookingsCommandService : CommandService<Booking, BookingState, BookingId> {
    public BookingsCommandService(IAggregateStore store, Services.IsRoomAvailable isRoomAvailable) : base(store) {
        OnNewAsync<BookRoom>(
            cmd => new BookingId(cmd.BookingId),
            (booking, cmd, _) => booking.BookRoom(
                cmd.GuestId,
                new RoomId(cmd.RoomId),
                new StayPeriod(LocalDate.FromDateTime(cmd.CheckInDate), LocalDate.FromDateTime(cmd.CheckOutDate)),
                new Money(cmd.BookingPrice, cmd.Currency),
                new Money(cmd.PrepaidAmount, cmd.Currency),
                DateTimeOffset.Now,
                isRoomAvailable
            )
        );

        OnExisting<RecordPayment>(
            cmd => new BookingId(cmd.BookingId),
            (booking, cmd) => booking.RecordPayment(
                new Money(cmd.PaidAmount, cmd.Currency),
                cmd.PaymentId,
                cmd.PaidBy,
                DateTimeOffset.Now
            )
        );
    }
}