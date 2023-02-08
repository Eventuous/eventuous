using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Sut.Domain;

public record BookingState : State<BookingState> {
    public BookingState() {
        On<RoomBooked>(
            (state, booked) => state with {
                Price = new Money(booked.Price),
                AmountPaid = new Money(0)
            }
        );

        On<BookingImported>((state, imported) => state with {
            Price = new Money(imported.Price),
            AmountPaid = new Money(0)
        });

        On<BookingPaymentRegistered>(
            (state, paid) => state with { AmountPaid = state.AmountPaid + new Money(paid.AmountPaid) }
        );
    }

    public Money Price      { get; private init; }
    public Money AmountPaid { get; private init; }

    public bool IsFullyPaid()
        => AmountPaid.Amount >= Price.Amount;

    public bool IsOverpaid()
        => AmountPaid.Amount > Price.Amount;
}
