using Eventuous.Sut.Domain;
using static Eventuous.Sut.App.Commands;

namespace Eventuous.Sut.App;

public class BookingService : CommandService<Booking, BookingState, BookingId> {
    public BookingService(IAggregateStore store, StreamNameMap? streamNameMap = null)
        : base(store, streamNameMap: streamNameMap) {
        On<BookRoom>()
            .InState(ExpectedState.New)
            .GetId(cmd => new BookingId(cmd.BookingId))
            .ActAsync(
                (booking, cmd, _) => {
                    booking.BookRoom(cmd.RoomId, new StayPeriod(cmd.CheckIn, cmd.CheckOut), new Money(cmd.Price));

                    return Task.CompletedTask;
                }
            );

        On<ImportBooking>()
            .InState(ExpectedState.New)
            .GetId(cmd => new BookingId(cmd.BookingId))
            .Act((booking, cmd) => booking.Import(cmd.RoomId, new StayPeriod(cmd.CheckIn, cmd.CheckOut), new Money(cmd.Price)));

        On<RecordPayment>()
            .InState(ExpectedState.Existing)
            .GetId(cmd => cmd.BookingId)
            .Act((booking, cmd) => booking.RecordPayment(cmd.PaymentId, cmd.Amount, cmd.PaidAt));
    }
}
