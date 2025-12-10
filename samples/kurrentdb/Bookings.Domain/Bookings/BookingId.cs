using Eventuous;

namespace Bookings.Domain.Bookings;

public record BookingId(string Value) : Id(Value);