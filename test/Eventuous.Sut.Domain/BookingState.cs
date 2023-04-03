using System.Collections.Immutable;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Sut.Domain;

public record BookingState : State<BookingState, BookingId> {
    public BookingState() {
        On<RoomBooked>(
            (state, booked) => state with {
                Price = new Money(booked.Price),
                AmountPaid = new Money(0)
            }
        );

        On<BookingImported>(
            (state, imported) => state with {
                Price = new Money(imported.Price),
                AmountPaid = new Money(0)
            }
        );

        On<BookingPaymentRegistered>(
            (state, paid) => state with {
                AmountPaid = state.AmountPaid + new Money(paid.AmountPaid),
                _registeredPayments = state._registeredPayments.Add(
                    new Payment(paid.PaymentId, new Money(paid.AmountPaid))
                )
            }
        );
    }

    ImmutableArray<Payment> _registeredPayments = ImmutableArray<Payment>.Empty;

    public Money Price      { get; private init; } = null!;
    public Money AmountPaid { get; private init; } = null!;

    public bool IsFullyPaid()
        => AmountPaid.Amount >= Price.Amount;

    public bool IsOverpaid()
        => AmountPaid.Amount > Price.Amount;

    public bool HasPayment(string paymentId)
        => _registeredPayments.Any(p => p.PaymentId == paymentId);

    record Payment(string PaymentId, Money Amount);
}
