using System.Collections.Immutable;
using Eventuous;
using static Bookings.Domain.Bookings.BookingEvents;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable NotAccessedPositionalProperty.Global

namespace Bookings.Domain.Bookings;

public record BookingState : State<BookingState> {
    public string     GuestId     { get; init; } = null!;
    public RoomId     RoomId      { get; init; } = null!;
    public StayPeriod Period      { get; init; } = null!;
    public Money      Price       { get; init; } = null!;
    public Money      Outstanding { get; init; } = null!;
    public bool       Paid        { get; init; }

    public ImmutableArray<PaymentRecord> Payments { get; init; } = ImmutableArray<PaymentRecord>.Empty;

    internal bool HasPaymentBeenRegistered(string paymentId) => Payments.Any(x => x.PaymentId == paymentId);

    public BookingState() {
        On<V1.RoomBooked>(HandleBooked);
        On<V1.PaymentRecorded>(HandlePayment);
        On<V1.BookingFullyPaid>((state, _) => state with { Paid = true });
    }

    static BookingState HandlePayment(BookingState state, V1.PaymentRecorded e)
        => state with {
            Outstanding = new() { Amount = e.Outstanding, Currency = e.Currency },
            Payments = state.Payments.Add(new(e.PaymentId, new(e.PaidAmount, e.Currency)))
        };

    static BookingState HandleBooked(BookingState state, V1.RoomBooked booked)
        => state with {
            RoomId = new(booked.RoomId),
            Period = new(booked.CheckInDate, booked.CheckOutDate),
            GuestId = booked.GuestId,
            Price = new() { Amount       = booked.BookingPrice, Currency      = booked.Currency },
            Outstanding = new() { Amount = booked.OutstandingAmount, Currency = booked.Currency }
        };
}

public record PaymentRecord(string PaymentId, Money PaidAmount);

public record DiscountRecord(Money Discount, string Reason);
