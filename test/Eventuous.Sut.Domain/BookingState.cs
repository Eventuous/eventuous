using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Sut.Domain;

public record BookingState : AggregateState<BookingState> {
    public BookingState() {
        On<RoomBooked>((state,      booked) => state with { Price = booked.Price });
        On<BookingImported>((state, imported) => state with { Price = imported.Price });

        On<BookingPaymentRegistered>(
            (state, paid) => state with { AmountPaid = state.AmountPaid + paid.AmountPaid }
        );
    }

    internal decimal Price      { get; private init; }
    internal decimal AmountPaid { get; private init; }

    public bool IsFullyPaid() => AmountPaid >= Price;

    public bool IsOverpaid() => AmountPaid > Price;
}
