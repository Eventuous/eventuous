using NodaTime;

namespace Eventuous.Sut.Domain;

public record StayPeriod {
    public StayPeriod(LocalDate checkIn, LocalDate checkOut) {
        if (checkIn >= checkOut)
            throw new ArgumentOutOfRangeException(nameof(checkOut), "Check out should be after check in");
            
        CheckIn  = checkIn;
        CheckOut = checkOut;
    }

    public LocalDate CheckIn  { get; }
    public LocalDate CheckOut { get; }
}