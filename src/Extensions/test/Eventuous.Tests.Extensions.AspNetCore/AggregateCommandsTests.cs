using Microsoft.AspNetCore.Mvc.Testing;

namespace Eventuous.Tests.Extensions.AspNetCore;

using Fixture;
using static SutBookingCommands;
using static Fixture.TestCommands;

public class AggregateCommandsTests(ITestOutputHelper output, WebApplicationFactory<Program> factory)
    : TestBaseWithLogs(output), IClassFixture<WebApplicationFactory<Program>> {
    readonly ITestOutputHelper _output = output;

    [Fact]
    public void RegisterAggregateCommands() {
        var builder = WebApplication.CreateBuilder();

        using var app = builder.Build();

        var b = app.MapDiscoveredCommands<BookingState>(typeof(BookRoom).Assembly);

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
    public void MapAggregateContractToCommandExplicitlyWithoutRouteWithWrongGenericAttr() {
        var act = () => new ServerFixture(
            factory,
            _output,
            _ => { },
            app => app
                .MapCommands<BookingState>()
                .MapCommand<ImportBookingHttp3, ImportBooking>(Enricher.EnrichCommand)
        );

        act.Should().Throw<InvalidOperationException>();
    }


    [Fact]
    public async Task MapAggregateContractToCommandExplicitly() {
        var fixture = new ServerFixture(
            factory,
            _output,
            _ => { },
            app => app
                .MapCommands<BookingState>()
                .MapCommand<ImportBookingHttp, ImportBooking>(ImportRoute, Enricher.EnrichCommand)
        );

        await Execute(fixture, ImportRoute);
    }

    [Fact]
    public async Task MapAggregateContractToCommandExplicitlyWithoutRoute() {
        var fixture = new ServerFixture(
            factory,
            _output,
            _ => { },
            app => app
                .MapCommands<BookingState>()
                .MapCommand<ImportBookingHttp1, ImportBooking>(Enricher.EnrichCommand)
        );

        await Execute(fixture, Import1Route);
    }

    [Fact]
    public async Task MapAggregateContractToCommandExplicitlyWithoutRouteWithGenericAttr() {
        var fixture = new ServerFixture(
            factory,
            _output,
            _ => { },
            app => app
                .MapCommands<BookingState>()
                .MapCommand<ImportBookingHttp2, ImportBooking>(Enricher.EnrichCommand)
        );

        await Execute(fixture, Import2Route);
    }

    [Fact]
    public async Task MapEnrichedCommand() {
        var fixture = new ServerFixture(
            factory,
            _output,
            _ => { },
            app => app
                .MapCommands<BookingState>()
                .MapCommand<BookRoom>((x, _) => x with { GuestId = TestData.GuestId })
        );
        var cmd      = fixture.GetBookRoom();
        var content = await fixture.ExecuteRequest<BookRoom, BookingState>(cmd, "book", cmd.BookingId);
        await VerifyJson(content);
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
        var content = await fixture.ExecuteRequest<ImportBookingHttp, BookingState>(import, route, bookRoom.BookingId);

        await VerifyJson(content);
    }
}
