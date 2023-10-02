namespace Eventuous.Tests.AspNetCore.Web.Fixture;

public static class Enricher {
    internal static SutBookingCommands.ImportBooking EnrichCommand(TestCommands.ImportBookingHttp command, HttpContext _)
        => new(
            new BookingId(command.BookingId),
            command.RoomId,
            new StayPeriod(command.CheckIn, command.CheckOut),
            new Money(command.Price)
        );
}
