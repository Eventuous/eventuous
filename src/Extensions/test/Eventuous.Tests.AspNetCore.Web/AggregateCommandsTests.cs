namespace Eventuous.Tests.AspNetCore.Web;

using Fixture;
using static SutBookingCommands;
using static Fixture.TestCommands;

public class AggregateCommandsTests(ITestOutputHelper output) : TestBaseWithLogs(output) {
    [Fact]
    public void RegisterAggregateCommands() {
        var builder = WebApplication.CreateBuilder();

        using var app = builder.Build();

        var b = app.MapDiscoveredCommands<Booking>(typeof(BookRoom).Assembly);

        b.DataSources.First().Endpoints[0].DisplayName.Should().Be("HTTP: POST book");
    }

    [Fact]
    public void RegisterAggregatesCommands() {
        var builder = WebApplication.CreateBuilder();

        using var app = builder.Build();

        var b = app.MapDiscoveredCommands(typeof(NestedCommands).Assembly);

        b.DataSources.First().Endpoints[0].DisplayName.Should().Be("HTTP: POST nested-book");
    }

    [Fact]
    public async Task MapAggregateContractToCommandExplicitlyWithoutRouteWithWrongGenericAttr() {
        using var fixture = new ServerFixture(
            output,
            configure: app => app
                .MapAggregateCommands<Booking>()
                .MapCommand<ImportBookingHttp3, ImportBooking>(Enricher.EnrichCommand)
        );

        var act = () => Execute(fixture, "import3");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task MapContractToCommandExplicitly() {
        using var fixture = new ServerFixture(
            output,
            configure: app => app.MapCommand<ImportBookingHttp, ImportBooking, Booking>(ImportRoute, Enricher.EnrichCommand)
        );

        await Execute(fixture, ImportRoute);
    }

    [Fact]
    public async Task MapAggregateContractToCommandExplicitly() {
        using var fixture = new ServerFixture(
            output,
            configure: app => app
                .MapAggregateCommands<Booking>()
                .MapCommand<ImportBookingHttp, ImportBooking>(ImportRoute, Enricher.EnrichCommand)
        );

        await Execute(fixture, ImportRoute);
    }

    [Fact]
    public async Task MapAggregateContractToCommandExplicitlyWithoutRoute() {
        using var fixture = new ServerFixture(
            output,
            configure: app => app
                .MapAggregateCommands<Booking>()
                .MapCommand<ImportBookingHttp1, ImportBooking>(Enricher.EnrichCommand)
        );

        await Execute(fixture, Import1Route);
    }

    [Fact]
    public async Task MapAggregateContractToCommandExplicitlyWithoutRouteWithGenericAttr() {
        using var fixture = new ServerFixture(
            output,
            configure: app => app
                .MapAggregateCommands<Booking>()
                .MapCommand<ImportBookingHttp2, ImportBooking>(Enricher.EnrichCommand)
        );

        await Execute(fixture, Import2Route);
    }

    [Fact]
    public async Task MapEnrichedCommand() {
        using var fixture = new ServerFixture(
            output,
            _ => { },
            app => app
                .MapAggregateCommands<Booking>()
                .MapCommand<BookRoom>((x, _) => x with { GuestId = TestData.GuestId })
        );
        var cmd      = fixture.GetBookRoom();
        var expected = new BookingEvents.RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price, TestData.GuestId);
        await fixture.ExecuteAndVerify<BookRoom, Booking>(cmd, "book", cmd.BookingId, expected);
    }

    static async Task Execute(ServerFixture fixture, string route) {
        var bookRoom = fixture.GetBookRoom();

        var import = new ImportBookingHttp(
            bookRoom.BookingId,
            bookRoom.RoomId,
            bookRoom.CheckIn,
            bookRoom.CheckOut,
            bookRoom.Price
        );
        var expected = new BookingEvents.BookingImported(import.RoomId, import.Price, import.CheckIn, import.CheckOut);
        await fixture.ExecuteAndVerify<ImportBookingHttp, Booking>(import, route, bookRoom.BookingId, expected);
    }
}
