using System.Collections.Immutable;
using NodaTime;

namespace Bookings.Application.Queries;

public record MyBookings {
    public ImmutableList<Booking> Bookings { get; init; } = [];

    public record Booking(string BookingId, LocalDate CheckInDate, LocalDate CheckOutDate, float Price);
}