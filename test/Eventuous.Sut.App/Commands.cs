using Eventuous.Sut.Domain;
using NodaTime;

namespace Eventuous.Sut.App;

public static class Commands {
    public record BookRoom(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price);

    public record ImportBooking {
        public string    BookingId { get; init; } = null!;
        public string    RoomId    { get; init; } = null!;
        public LocalDate CheckIn   { get; init; }
        public LocalDate CheckOut  { get; init; }
        public float     Price     { get; init; }
    }

    public record RecordPayment(BookingId BookingId, string PaymentId, Money Amount, DateTimeOffset PaidAt);
}
