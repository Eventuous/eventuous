using System.Collections.Immutable;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Sut.Domain;

public record BookingState : AggregateState<BookingState, BookingId> {
    public BookingState() {
        On<RoomBooked>((state, booked) => state with { Id = new BookingId(booked.BookingId), Price = booked.Price });
        On<BookingImported>((state, imported) => state with { Id = new BookingId(imported.BookingId) });

        On<BookingPaymentRegistered>(
            (state, paid) => state with {
                PaymentRecords = state.PaymentRecords.Add(new PaymentRecord(paid.PaymentId, paid.AmountPaid)),
                AmountPaid = state.AmountPaid + paid.AmountPaid
            }
        );
    }

    decimal                      Price          { get; init; }
    decimal                      AmountPaid     { get; init; }
    ImmutableList<PaymentRecord> PaymentRecords { get; init; } = ImmutableList<PaymentRecord>.Empty;

    public bool HasPaymentRecord(string paymentId) => PaymentRecords.Any(x => x.PaymentId == paymentId);

    public bool IsFullyPaid() => AmountPaid >= Price;

    public bool IsOverpaid() => AmountPaid > Price;

    record PaymentRecord(string PaymentId, decimal Amount);
}