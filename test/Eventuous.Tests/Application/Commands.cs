using NodaTime;

namespace Eventuous.Tests.Application {
    public static class Commands {
        public record BookRoom(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, decimal Price);
    }
}