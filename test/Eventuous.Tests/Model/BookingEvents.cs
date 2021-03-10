using NodaTime;

namespace Eventuous.Tests.Model {
    public static class BookingEvents {
        public record RoomBooked(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, decimal Price);

        public record BookingPaid(string BookingId, decimal AmountPaid, bool PaidInFull);

        public record BookingCancelled(string BookingId);

        public static void MapBookingEvents() {
            TypeMap.AddType<RoomBooked>("RoomBooked");
            TypeMap.AddType<BookingPaid>("BookingPaid");
            TypeMap.AddType<BookingCancelled>("BookingCancelled");
        }
    }
}