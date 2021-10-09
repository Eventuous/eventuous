using System.Collections.Immutable;

namespace Eventuous.Sut.Domain;

public record BookingState : AggregateState<BookingState, BookingId> {
    decimal                      Price          { get; init; }
    decimal                      AmountPaid     { get; init; }
    ImmutableList<PaymentRecord> PaymentRecords { get; init; } = ImmutableList<PaymentRecord>.Empty;

    public bool HasPaymentRecord(string paymentId) => PaymentRecords.Any(x => x.PaymentId == paymentId);

    public bool IsFullyPaid() => AmountPaid >= Price;

    public bool IsOverpaid() => AmountPaid > Price;

    public override BookingState When(object @event)
        => @event switch {
            BookingEvents.RoomBooked booked        => this with { Id = new BookingId(booked.BookingId), Price = booked.Price },
            BookingEvents.BookingImported imported => this with { Id = new BookingId(imported.BookingId) },
            BookingEvents.BookingPaymentRegistered paid => this with {
                PaymentRecords = PaymentRecords.Add(new PaymentRecord(paid.PaymentId, paid.AmountPaid)),
                AmountPaid = AmountPaid + paid.AmountPaid
            },
            _ => this
        };

    record PaymentRecord(string PaymentId, decimal Amount);
}