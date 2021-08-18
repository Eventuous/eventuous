using NodaTime;
// ReSharper disable MemberHidesStaticFromOuterClass

namespace Eventuous.Tests.SutDomain {
    public static class BookingEvents {
        public record RoomBooked(
            string    BookingId,
            string    RoomId,
            LocalDate CheckIn,
            LocalDate CheckOut,
            decimal   Price
        );

        public record BookingPaymentRegistered(
            string  BookingId,
            string  PaymentId,
            decimal AmountPaid
        );

        public record BookingFullyPaid(string BookingId);

        [EventType(TypeNames.BookingCancelled)]
        public record BookingCancelled(string BookingId);

        [EventType(TypeNames.BookingImported)]
        public record BookingImported(
            string    BookingId,
            string    RoomId,
            LocalDate CheckIn,
            LocalDate CheckOut
        );

        public static void MapBookingEvents() {
            TypeMap.AddType<RoomBooked>("RoomBooked");
            TypeMap.AddType<BookingPaymentRegistered>("BookingPaymentRegistered");
        }

        public static class TypeNames {
            public const string BookingCancelled = "V1.BookingCancelled";
            public const string BookingImported  = "V1.BookingImported";
        }
    }
}
