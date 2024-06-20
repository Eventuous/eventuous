using Eventuous.Projections.MongoDB.Tools;
using NodaTime;

// ReSharper disable CollectionNeverUpdated.Global

namespace Bookings.Application.Queries;

public record MyBookings(string Id) : ProjectedDocument(Id) {
    public List<Booking> Bookings { get; init; } = [];

    public record Booking(string BookingId, LocalDate CheckInDate, LocalDate CheckOutDate, float Price);
}
