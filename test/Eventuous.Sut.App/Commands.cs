using NodaTime;

namespace Eventuous.Sut.App;

public static class Commands {
    public record BookRoom(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, decimal Price);

    public record ImportBooking(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut);
    
    public record RecordPayment(string BookingId, string PaymentId, decimal Amount);
}