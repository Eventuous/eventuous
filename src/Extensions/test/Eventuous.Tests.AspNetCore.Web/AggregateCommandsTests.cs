using Microsoft.AspNetCore.Mvc.Testing;

namespace Eventuous.Tests.AspNetCore.Web;

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
    public void MapAggregateContractToCommandExplicitlyWithoutRouteWithWrongGenericAttr() {
        var act = () => new ServerFixture(
            factory,
            _output,
            _ => { },
            app => app
                .MapAggregateCommands<Booking, BookingResult>()
                .MapCommand<ImportBookingHttp3, ImportBooking>(Enricher.EnrichCommand)
        );

        act.Should().Throw<InvalidOperationException>();
    }

    public static IEnumerable<object[]> ResultTypesToTest() {
        yield return [new BookingResult()];
        yield return [new Result()];
    }

    [Theory]
    [MemberData(nameof(ResultTypesToTest))]
    public async Task MapContractToCommandExplicitly<TResult>(TResult tResult)
        where TResult : Result, new() {
        var fixture = new ServerFixture(
            factory,
            _output,
            _ => { },
            app => app.MapCommand<ImportBookingHttp, ImportBooking, Booking, TResult>(ImportRoute, Enricher.EnrichCommand)
        );

        var resultTypeName = tResult.GetType().Name;
        await Execute(fixture, ImportRoute, resultTypeName);
    }

    [Theory]
    [MemberData(nameof(ResultTypesToTest))]
    public async Task MapAggregateContractToCommandExplicitly<TResult>(TResult tResult)
        where TResult : Result, new() {
        var fixture = new ServerFixture(
            factory,
            _output,
            _ => { },
            app => app
                .MapAggregateCommands<Booking, TResult>()
                .MapCommand<ImportBookingHttp, ImportBooking>(ImportRoute, Enricher.EnrichCommand)
        );

        var resultTypeName = tResult.GetType().Name;
        await Execute(fixture, ImportRoute, resultTypeName);
    }

    [Fact]
    public async Task MapAggregateContractToCommandExplicitlyWithoutRoute() {
        var fixture = new ServerFixture(
            factory,
            _output,
            _ => { },
            app => app
                .MapAggregateCommands<Booking, BookingResult>()
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
                .MapAggregateCommands<Booking, BookingResult>()
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
                .MapAggregateCommands<Booking, BookingResult>()
                .MapCommand<BookRoom>((x, _) => x with { GuestId = TestData.GuestId })
        );
        var cmd      = fixture.GetBookRoom();
        var content = await fixture.ExecuteRequest<BookRoom, Booking>(cmd, "book", cmd.BookingId);
        await VerifyJson(content);
    }

    static async Task Execute(ServerFixture fixture, string route, string typeName = "") {
        var bookRoom = fixture.GetBookRoom();

        var import = new ImportBookingHttp(
            bookRoom.BookingId,
            bookRoom.RoomId,
            bookRoom.CheckIn,
            bookRoom.CheckOut,
            bookRoom.Price
        );
        var content = await fixture.ExecuteRequest<ImportBookingHttp, Booking>(import, route, bookRoom.BookingId);

        await VerifyJson(content).UseParameters(typeName);
    }
}
