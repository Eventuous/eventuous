using Microsoft.AspNetCore.Mvc.Testing;

namespace Eventuous.Tests.Extensions.AspNetCore;

using Fixture;
using static SutBookingCommands;
using static Fixture.TestCommands;

[ClassDataSource<WebApplicationFactory<Program>>]
public class AggregateCommandsTests(WebApplicationFactory<Program> factory) : TestBaseWithLogs {
    [Test]
    public async Task RegisterAggregateCommands() {
        var builder = WebApplication.CreateBuilder();

        await using var app = builder.Build();

        var b = app.MapDiscoveredCommands<BookingState>(typeof(BookRoom).Assembly);

        await Assert.That(b.DataSources.First().Endpoints[0].DisplayName).IsEqualTo("HTTP: POST book");
    }

    [Test]
    public async Task RegisterAggregatesCommands() {
        var builder = WebApplication.CreateBuilder();

        await using var app = builder.Build();

        var b = app.MapDiscoveredCommands(typeof(NestedCommands).Assembly);

        await Assert.That(b.DataSources.First().Endpoints[0].DisplayName).IsEqualTo("HTTP: POST nested-book");
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
