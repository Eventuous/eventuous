using NodaTime;

// ReSharper disable MemberHidesStaticFromOuterClass

namespace Eventuous.Sut.Domain;

public static class BookingEvents {
    [EventType(TypeNames.RoomBooked)]
    public record RoomBooked(string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price, string? GuestId = null);

    [EventType(TypeNames.PaymentRegistered)]
    public record BookingPaymentRegistered(string PaymentId, float AmountPaid);

    [EventType(TypeNames.OutstandingAmountChanged)]
    public record BookingOutstandingAmountChanged(float OutstandingAmount);

    [EventType(TypeNames.BookingFullyPaid)]
    public record BookingFullyPaid(DateTimeOffset PaidAt);

    [EventType(TypeNames.BookingOverpaid)]
    public record BookingOverpaid(float OverpaidAmount);

    [EventType(TypeNames.BookingCancelled)]
    public record BookingCancelled;

    [EventType(TypeNames.BookingImported)]
    public record BookingImported(string RoomId, float Price, LocalDate CheckIn, LocalDate CheckOut);

    [EventType(TypeNames.Executed)]
    public record Executed;

    // These constants are for test purpose, use inline names in real apps
    public static class TypeNames {
        public const string BookingCancelled         = "V1.BookingCancelled";
        public const string BookingImported          = "V1.BookingImported";
        public const string RoomBooked               = "V1.RoomBooked";
        public const string PaymentRegistered        = "V1.PaymentRegistered";
        public const string OutstandingAmountChanged = "V1.OutstandingAmountChanged";
        public const string BookingFullyPaid         = "V1.BookingFullyPaid";
        public const string BookingOverpaid          = "V1.BookingOverpaid";
        public const string Executed                 = "V1.Executed";
    }
}
