using NodaTime;

namespace Eventuous.Tests.Model {
    public static class BookingEvents {
        public record RoomBooked(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, decimal Price);

        public record BookingPaymentRegistered(string BookingId, string PaymentId, decimal AmountPaid);

        public record BookingFullyPaid(string BookingId);

        public record BookingCancelled(string BookingId);

        public record BookingImported(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut);

        public static void MapBookingEvents() {
            TypeMap.AddType<RoomBooked>("RoomBooked");
            TypeMap.AddType<BookingPaymentRegistered>("BookingPaymentRegistered");
            TypeMap.AddType<BookingCancelled>("BookingCancelled");
            TypeMap.AddType<BookingImported>("BookingImported");
        }
    }
}