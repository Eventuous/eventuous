// ReSharper disable AccessToDisposedClosure

namespace Eventuous.Tests.AspNetCore.Web;

using TestHelpers;
using Fixture;

public class MappedCommandTests : IDisposable {
    readonly TestEventListener _listener;

    public MappedCommandTests(ITestOutputHelper output) {
        _listener = new TestEventListener(output);
    }

    [Fact]
    public async Task MapContractToCommandExplicitly() {
        using var fixture = new ServerFixture(
            configure: app => app
                .MapCommand<ImportBookingHttp, ImportBooking, Booking>("import", EnrichCommand)
        );

        await Execute(fixture, "import");
    }

    [Fact]
    public async Task MapAggregateContractToCommandExplicitly() {
        using var fixture = new ServerFixture(
            configure: app => app
                .MapAggregateCommands<Booking>()
                .MapCommand<ImportBookingHttp, ImportBooking>("import", EnrichCommand)
        );

        await Execute(fixture, "import");
    }

    [Fact]
    public async Task MapAggregateContractToCommandExplicitlyWithoutRoute() {
        using var fixture = new ServerFixture(
            configure: app => app
                .MapAggregateCommands<Booking>()
                .MapCommand<ImportBookingHttp1, ImportBooking>(EnrichCommand)
        );

        await Execute(fixture, "import1");
    }

    [Fact]
    public async Task MapAggregateContractToCommandExplicitlyWithoutRouteWithGenericAttr() {
        using var fixture = new ServerFixture(
            configure: app => app
                .MapAggregateCommands<Booking>()
                .MapCommand<ImportBookingHttp2, ImportBooking>(EnrichCommand)
        );

        await Execute(fixture, "import2");
    }

    [Fact]
    public async Task MapAggregateContractToCommandExplicitlyWithoutRouteWithWrongGenericAttr() {
        using var fixture = new ServerFixture(
            configure: app => app
                .MapAggregateCommands<Booking>()
                .MapCommand<ImportBookingHttp3, ImportBooking>(EnrichCommand)
        );

        Func<Task> act = () => Execute(fixture, "import3");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    static ImportBooking EnrichCommand(ImportBookingHttp command, HttpContext _)
        => new(
            new BookingId(command.BookingId),
            command.RoomId,
            new StayPeriod(command.CheckIn, command.CheckOut),
            new Money(command.Price)
        );

    static async Task Execute(ServerFixture fixture, string route) {
        var bookRoom = fixture.GetBookRoom();

        using var client = fixture.GetClient();

        var import = new ImportBookingHttp(
            bookRoom.BookingId,
            bookRoom.RoomId,
            bookRoom.CheckIn,
            bookRoom.CheckOut,
            bookRoom.Price
        );

        var request  = new RestRequest(route).AddJsonBody(import);
        var response = await client.ExecutePostAsync<OkResult>(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var expected = new BookingEvents.BookingImported(import.RoomId, import.Price, import.CheckIn, import.CheckOut);

        var events = await fixture.ReadStream<Booking>(bookRoom.BookingId);
        var last   = events.LastOrDefault();
        last.Payload.Should().BeEquivalentTo(expected);
    }

    public void Dispose()
        => _listener.Dispose();
}

record ImportBookingHttp(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price);

[HttpCommand(Route = "import1")]
record ImportBookingHttp1(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price)
    : ImportBookingHttp(BookingId, RoomId, CheckIn, CheckOut, Price);

[HttpCommand<Booking>(Route = "import2")]
record ImportBookingHttp2(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price)
    : ImportBookingHttp(BookingId, RoomId, CheckIn, CheckOut, Price);

[HttpCommand<Brooking>(Route = "import_wrong")]
record ImportBookingHttp3(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price)
    : ImportBookingHttp(BookingId, RoomId, CheckIn, CheckOut, Price);

class Brooking : Aggregate {
    public override void Load(IEnumerable<object?> events) { }
    public override void Load(Snapshot snapshot) => throw new NotImplementedException();
}
