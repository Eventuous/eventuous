using Eventuous.Sut.Domain;

namespace Eventuous.Sut.App;

using static Commands;

public class BookingService : CommandService<Booking, BookingState, BookingId> {
    public BookingService(
            IEventStore    eventStore,
            StreamNameMap? streamNameMap = null,
            ITypeMapper?   typeMapper    = null,
            AmendEvent?    amendEvent    = null
        )
        : base(eventStore, streamNameMap: streamNameMap, typeMap: typeMapper, amendEvent: amendEvent) {
        On<BookRoom>()
            .InState(ExpectedState.New)
            .GetId(cmd => new(cmd.BookingId))
            .ActAsync(
                (booking, cmd, _) => {
                    booking.BookRoom(cmd.RoomId, new(cmd.CheckIn, cmd.CheckOut), new(cmd.Price));

                    return Task.CompletedTask;
                }
            );

        On<ImportBooking>()
            .InState(ExpectedState.New)
            .GetId(cmd => new(cmd.BookingId))
            .Act((booking, cmd) => booking.Import(cmd.RoomId, new(cmd.CheckIn, cmd.CheckOut), new(cmd.Price)));

        On<RecordPayment>()
            .InState(ExpectedState.Existing)
            .GetId(cmd => cmd.BookingId)
            .Act((booking, cmd) => booking.RecordPayment(cmd.PaymentId, cmd.Amount, cmd.PaidAt));

        On<CancelBooking>()
            .InState(ExpectedState.Any)
            .GetId(cmd => cmd.BookingId)
            .Act((booking, _) => booking.Cancel());
        
        On<ExecuteNoMatterWhat>()
            .InState(ExpectedState.New)
            .GetId(cmd => new(cmd.BookingId))
            .Act((booking, _) => booking.Execute())
            .AmendAppend((append, _) => append with { ExpectedVersion = ExpectedStreamVersion.Any });
    }
}
