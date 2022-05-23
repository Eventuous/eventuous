using NodaTime;

// ReSharper disable MemberHidesStaticFromOuterClass

namespace Eventuous.Sut.Domain;

public static class BookingEvents {
    [EventType("RoomBooked")]
    public record RoomBooked(
        string    BookingId,
        string    RoomId,
        LocalDate CheckIn,
        LocalDate CheckOut,
        decimal   Price,
        string?   GuestId = null
    );

    [EventType("PaymentRegistered")]
    public record BookingPaymentRegistered(
        string  BookingId,
        string  PaymentId,
        decimal AmountPaid
    );
    
    [EventType("OutstandingAmountChanged")]
    public record BookingOutstandingAmountChanged(string BookingId, decimal OutstandingAmount);

    [EventType("BookingFullyPaid")]
    public record BookingFullyPaid(string BookingId);
    
    [EventType("BookingOverpaid")]
    public record BookingOverpaid(string BookingId, decimal OverpaidAmount);

    [EventType(TypeNames.BookingCancelled)]
    public record BookingCancelled(string BookingId);

    [EventType("V1.BookingImported")]
    public record BookingImported(
        string    BookingId,
        string    RoomId,
        LocalDate CheckIn,
        LocalDate CheckOut
    );

    // These constants are for test purpose, use inline names in real apps
    public static class TypeNames {
        public const string BookingCancelled = "V1.BookingCancelled";
    }

    public static void MapBookingEvents() => TypeMap.RegisterKnownEventTypes();
}
