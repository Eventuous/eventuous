using Eventuous.Sut.Domain;

namespace Eventuous.Sut.App;

public class BookingServiceWithStream : ApplicationService<Booking, BookingState, BookingId> {
    public BookingServiceWithStream(IAggregateStore store) : base(store) {
        OnNew<Commands.BookRoom>(
            cmd => GetStreamName(cmd.BookingId),
            (booking, cmd)
                => booking.BookRoom(
                    new BookingId(cmd.BookingId),
                    cmd.RoomId,
                    new StayPeriod(cmd.CheckIn, cmd.CheckOut),
                    cmd.Price
                )
        );

        OnAny<Commands.ImportBooking>(
            cmd => GetStreamName(cmd.BookingId),
            (booking, cmd)
                => booking.Import(
                    new BookingId(cmd.BookingId),
                    cmd.RoomId,
                    new StayPeriod(cmd.CheckIn, cmd.CheckOut)
                )
        );
    }
    
    public static StreamName GetStreamName(string bookingId) => new($"hotel-booking-{bookingId}");
}