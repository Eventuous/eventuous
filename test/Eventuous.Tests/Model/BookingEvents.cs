using NodaTime;

namespace Eventuous.Tests.Model {
    public static class BookingEvents {
        public record RoomBooked(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, decimal Price);

        public record BookingPaid(string BookingId, decimal AmountPaid, bool PaidInFull);

        public record BookingCancelled(string BookingId);

        public record BookingImported(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut);

        public static void MapBookingEvents() {
            TypeMap.AddType<RoomBooked>("RoomBooked");
            TypeMap.AddType<BookingPaid>("BookingPaid");
            TypeMap.AddType<BookingCancelled>("BookingCancelled");
            TypeMap.AddType<BookingImported>("BookingImported");
        }
    }
}