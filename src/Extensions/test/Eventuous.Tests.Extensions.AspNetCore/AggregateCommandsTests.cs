using Microsoft.AspNetCore.Mvc.Testing;

namespace Eventuous.Tests.Extensions.AspNetCore;

using Fixture;
using static SutBookingCommands;
using static Fixture.TestCommands;

[ClassDataSource<WebApplicationFactory<Program>>]
public class AggregateCommandsTests(WebApplicationFactory<Program> factory) : TestBaseWithLogs {
    [Test]
    public async Task MapContractExplicitly() {
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
    public async Task MapContractExplicitlyWithoutRoute() {
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
    public async Task MapContractExplicitlyWithoutRouteWithGenericAttr() {
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
    public void MapContractExplicitlyWithoutRouteWithWrongGenericAttr() {
        Assert.Throws<InvalidOperationException>(Act);

        return;

        void Act() {
            _ = new ServerFixture(
                factory,
                _ => { },
#pragma warning disable EVTA001
                app => app
                    .MapCommands<BookingState>()
                    .MapCommand<ImportBookingHttp3, ImportBooking>(Enricher.EnrichCommand)
#pragma warning restore EVTA001
            );
        }
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
        await VerifyJson(content).IgnoreParameters();
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

        await VerifyJson(content).IgnoreParameters();
    }
}
