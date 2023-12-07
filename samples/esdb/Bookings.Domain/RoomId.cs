using Eventuous;

namespace Bookings.Domain;

public record RoomId(string Value) : AggregateId(Value);