namespace Eventuous.Tests.AspNetCore.Web.Fixture;

public static class TestCommands {
    public const string ImportRoute = "import";
    public const string Import1Route = "import1";
    public const string Import2Route = "import2";
    public const string ImportWrongRoute = "import-wrong";
    
    public record ImportBookingHttp(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price);

    [HttpCommand(Route = Import1Route)]
    public record ImportBookingHttp1(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price)
        : ImportBookingHttp(BookingId, RoomId, CheckIn, CheckOut, Price);

    [HttpCommand<Booking>(Route = Import2Route)]
    public record ImportBookingHttp2(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price)
        : ImportBookingHttp(BookingId, RoomId, CheckIn, CheckOut, Price);

    [HttpCommand<Brooking>(Route = ImportWrongRoute)]
    public record ImportBookingHttp3(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price)
        : ImportBookingHttp(BookingId, RoomId, CheckIn, CheckOut, Price);
}
