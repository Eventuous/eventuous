using Eventuous.Sut.App;
using NodaTime;

namespace Eventuous.Tests.Application;

public static class Helpers {
    public static Commands.BookRoom GetBookRoom(string bookingId = "123") {
        var checkIn  = LocalDate.FromDateTime(DateTime.Today);
        var checkOut = checkIn.PlusDays(1);

        return new(bookingId, "234", checkIn, checkOut, 100);
    }
}
