namespace Eventuous.Tests.Application;

using Sut.Domain;
using static Sut.App.Commands;
using static Sut.Domain.BookingEvents;

public class BookingFuncService : CommandService<BookingState> {
    public BookingFuncService(IEventStore store, ITypeMapper? typeMap = null, AmendEvent? amendEvent = null) : base(store, typeMap, amendEvent) {
        On<BookRoom>()
            .InState(ExpectedState.New)
            .GetStream(cmd => GetStream(cmd.BookingId))
            .Act(cmd => [new RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price)]);

        On<RecordPayment>()
            .InState(ExpectedState.Existing)
            .GetStream(cmd => GetStream(cmd.BookingId))
            .Act(RecordPayment);

        On<ImportBooking>()
            .InState(ExpectedState.Any)
            .GetStream(cmd => GetStream(cmd.BookingId))
            .Act((_, _, cmd) => [new BookingImported(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut)]);

        On<CancelBooking>()
            .InState(ExpectedState.Any)
            .GetStream(cmd => GetStream(cmd.BookingId))
            .Act((_, _, _) => [new BookingCancelled()]);

        On<ExecuteNoMatterWhat>()
            .InState(ExpectedState.New)
            .GetStream(cmd => GetStream(cmd.BookingId))
            .Act((_, _, _) => [new Executed()])
            .AmendAppend((append, _) => append with { ExpectedVersion = ExpectedStreamVersion.Any });

        return;

        static IEnumerable<object> RecordPayment(BookingState state, object[] originalEvents, RecordPayment cmd) {
            if (state.HasPayment(cmd.PaymentId)) yield break;

            var registered = new BookingPaymentRegistered(cmd.PaymentId, cmd.Amount.Amount);

            yield return registered;

            var newState = state.When(registered);

            if (state.AmountPaid != newState.AmountPaid) {
                yield return (new BookingOutstandingAmountChanged((state.Price - newState.AmountPaid).Amount));
            }

            if (newState.IsFullyPaid()) yield return new BookingFullyPaid(cmd.PaidAt);
            if (newState.IsOverpaid()) yield return new BookingOverpaid((state.AmountPaid - state.Price).Amount);
        }
    }
}
