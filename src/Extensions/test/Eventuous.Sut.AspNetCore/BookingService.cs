using Eventuous.AspNetCore.Web;
using Eventuous.Sut.Domain;
using NodaTime;

namespace Eventuous.Sut.AspNetCore;

public class BookingService : ApplicationService<Booking, BookingState, BookingId> {
    public BookingService(IAggregateStore store, StreamNameMap? streamNameMap = null)
        : base(store, streamNameMap: streamNameMap)
        => OnNew<BookRoom>(
            cmd => new BookingId(cmd.BookingId),
            (booking, cmd)
                => booking.BookRoom(
                    cmd.RoomId,
                    new StayPeriod(cmd.CheckIn, cmd.CheckOut),
                    cmd.Price,
                    cmd.GuestId
                )
        );
}

[HttpCommand(Route = "book")]
record BookRoom(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, decimal Price, string? GuestId);
