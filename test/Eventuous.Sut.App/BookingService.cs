using Eventuous.Sut.Domain;
using static Eventuous.Sut.App.Commands;

namespace Eventuous.Sut.App;

public class BookingService : ApplicationService<Booking, BookingState, BookingId> {
    public BookingService(IAggregateStore store, StreamNameMap? streamNameMap = null)
        : base(store, streamNameMap: streamNameMap) {
        OnNewAsync<BookRoom>(
            cmd => new BookingId(cmd.BookingId),
            (booking, cmd, _)
                => {
                booking.BookRoom(
                    cmd.RoomId,
                    new StayPeriod(cmd.CheckIn, cmd.CheckOut),
                    cmd.Price
                );

                return Task.CompletedTask;
            }
        );

        OnAny<ImportBooking>(
            cmd => new BookingId(cmd.BookingId),
            (booking, cmd)
                => booking.Import(
                    cmd.RoomId,
                    new StayPeriod(cmd.CheckIn, cmd.CheckOut),
                    cmd.Price
                )
        );

        OnExisting<RecordPayment>(
            cmd => new BookingId(cmd.BookingId),
            (booking, cmd) => booking.RecordPayment(cmd.PaymentId, cmd.Amount, cmd.PaidAt)
        );
    }
}