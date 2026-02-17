using Eventuous;
using NodaTime;

namespace Bookings.Domain;

public record StayPeriod {
    public LocalDate CheckIn  { get; }
    public LocalDate CheckOut { get; }

    internal StayPeriod() { }

    public StayPeriod(LocalDate checkIn, LocalDate checkOut) {
        if (checkIn > checkOut) throw new DomainException("Check in date must be before check out date");

        (CheckIn, CheckOut) = (checkIn, checkOut);
    }
}