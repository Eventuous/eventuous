using NodaTime;

namespace Eventuous.Sut.App;

public static class Commands {
    public record BookRoom(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, decimal Price);

    public record ImportBooking {
        public string    BookingId { get; init; }
        public string    RoomId    { get; init; }
        public LocalDate CheckIn   { get; init; }
        public LocalDate CheckOut  { get; init; }
        public decimal   Price     { get; init; }
    }

    public record RecordPayment(string BookingId, string PaymentId, decimal Amount, DateTimeOffset PaidAt);
}