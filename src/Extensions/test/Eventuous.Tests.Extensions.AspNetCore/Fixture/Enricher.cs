namespace Eventuous.Tests.Extensions.AspNetCore.Fixture;

public static class Enricher {
    internal static SutBookingCommands.ImportBooking EnrichCommand(TestCommands.ImportBookingHttp command, HttpContext _)
        => new(new(command.BookingId), command.RoomId, new(command.CheckIn, command.CheckOut), new(command.Price));
}
