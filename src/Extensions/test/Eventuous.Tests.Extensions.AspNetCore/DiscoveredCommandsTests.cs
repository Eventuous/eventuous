using Microsoft.AspNetCore.Mvc.Testing;
using static Eventuous.Sut.AspNetCore.SutBookingCommands.NestedCommands;

namespace Eventuous.Tests.Extensions.AspNetCore;

using static SutBookingCommands;
using Fixture;

[ClassDataSource<WebApplicationFactory<Program>>]
public class DiscoveredCommandsTests(WebApplicationFactory<Program> factory) : TestBaseWithLogs() {
    [Test]
    public async Task CallDiscoveredCommandRoute() {
        var fixture = new ServerFixture(
            factory,
            _ => { },
            app => app.MapDiscoveredCommands(typeof(NestedCommands).Assembly)
        );

        var cmd          = fixture.GetNestedBookRoom(new DateTime(2023, 10, 1));
        var streamEvents = await fixture.ExecuteRequest<NestedBookRoom, BookingState>(cmd, NestedBookRoute, cmd.BookingId);
        await VerifyJson(streamEvents);
    }
}
