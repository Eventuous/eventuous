using Eventuous.Projections.MongoDB.Tools;
using NodaTime;

namespace Bookings.Application.Queries;

public record MyBookings : ProjectedDocument {
    public MyBookings(string id) : base(id) { }

    public List<Booking> Bookings { get; init; } = new();

    public record Booking(string BookingId, LocalDate CheckInDate, LocalDate CheckOutDate, float Price);
}