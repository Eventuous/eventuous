using Eventuous.Tests.AspNetCore.Web.Fixture;

namespace Eventuous.Tests.AspNetCore.Web;

using static SutBookingCommands;

public class DiscoveredCommandsTests(ITestOutputHelper output) : TestBaseWithLogs(output) {
    [Fact]
    public async Task CallDiscoveredCommandRoute() {
        using var fixture = new ServerFixture(
            output,
            _ => { },
            app => app.MapDiscoveredCommands(typeof(NestedCommands).Assembly)
        );

        var cmd          = fixture.GetNestedBookRoom(new DateTime(2023, 10, 1));
        var streamEvents = await fixture.ExecuteAndRead<NestedCommands.NestedBookRoom, Booking>(cmd, NestedBookRoute, cmd.BookingId);
        await Verify(streamEvents);
    }
}
