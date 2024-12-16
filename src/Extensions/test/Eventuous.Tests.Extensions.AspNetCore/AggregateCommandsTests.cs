using Microsoft.AspNetCore.Mvc.Testing;

namespace Eventuous.Tests.Extensions.AspNetCore;

using Fixture;
using static SutBookingCommands;
using static Fixture.TestCommands;

[ClassDataSource<WebApplicationFactory<Program>>]
public class AggregateCommandsTests(WebApplicationFactory<Program> factory) : TestBaseWithLogs() {
    [Test]
    public void RegisterAggregateCommands() {
        var builder = WebApplication.CreateBuilder();

        using var app = builder.Build();

        var b = app.MapDiscoveredCommands<BookingState>(typeof(BookRoom).Assembly);

        b.DataSources.First().Endpoints[0].DisplayName.Should().Be("HTTP: POST book");
    }

    [Test]
    public void RegisterAggregatesCommands() {
        var builder = WebApplication.CreateBuilder();

        using var app = builder.Build();

        var b = app.MapDiscoveredCommands(typeof(NestedCommands).Assembly);

        b.DataSources.First().Endpoints[0].DisplayName.Should().Be("HTTP: POST nested-book");
    }

    [Test]
    public void MapAggregateContractToCommandExplicitlyWithoutRouteWithWrongGenericAttr() {
        var act = () => new ServerFixture(
            factory,
            _ => { },
            app => app
                .MapCommands<BookingState>()
                .MapCommand<ImportBookingHttp3, ImportBooking>(Enricher.EnrichCommand)
        );

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public async Task MapAggregateContractToCommandExplicitly() {
        var fixture = new ServerFixture(
            factory,
            _ => { },
            app => app
                .MapCommands<BookingState>()
                .MapCommand<ImportBookingHttp, ImportBooking>(ImportRoute, Enricher.EnrichCommand)
        );

        await Execute(fixture, ImportRoute);
    }

    [Test]
    public async Task MapAggregateContractToCommandExplicitlyWithoutRoute() {
        var fixture = new ServerFixture(
            factory,
            _ => { },
            app => app
                .MapCommands<BookingState>()
                .MapCommand<ImportBookingHttp1, ImportBooking>(Enricher.EnrichCommand)
        );

        await Execute(fixture, Import1Route);
    }

    [Test]
    public async Task MapAggregateContractToCommandExplicitlyWithoutRouteWithGenericAttr() {
        var fixture = new ServerFixture(
            factory,
            _ => { },
            app => app
                .MapCommands<BookingState>()
                .MapCommand<ImportBookingHttp2, ImportBooking>(Enricher.EnrichCommand)
        );

        await Execute(fixture, Import2Route);
    }

    [Test]
    public async Task MapEnrichedCommand() {
        var fixture = new ServerFixture(
            factory,
            _ => { },
            app => app
                .MapCommands<BookingState>()
                .MapCommand<BookRoom>((x, _) => x with { GuestId = TestData.GuestId })
        );
        var cmd     = ServerFixture.GetBookRoom();
        var content = await fixture.ExecuteRequest<BookRoom, BookingState>(cmd, "book", cmd.BookingId);
        await VerifyJson(content);
    }

    static async Task Execute(ServerFixture fixture, string route) {
        var bookRoom = ServerFixture.GetBookRoom();

        var import = new ImportBookingHttp(
            bookRoom.BookingId,
            bookRoom.RoomId,
            bookRoom.CheckIn,
            bookRoom.CheckOut,
            bookRoom.Price
        );
        var content = await fixture.ExecuteRequest<ImportBookingHttp, BookingState>(import, route, bookRoom.BookingId);

        await VerifyJson(content);
    }
}
