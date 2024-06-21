using Microsoft.AspNetCore.Mvc.Testing;
using static Eventuous.Sut.AspNetCore.SutBookingCommands.NestedCommands;

namespace Eventuous.Tests.AspNetCore.Web;

using static SutBookingCommands;
using Fixture;

public class DiscoveredCommandsTests(ITestOutputHelper output, WebApplicationFactory<Program> factory)
    : TestBaseWithLogs(output), IClassFixture<WebApplicationFactory<Program>> {
    readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task CallDiscoveredCommandRoute() {
        var fixture = new ServerFixture(
            factory,
            _output,
            _ => { },
            app => app.MapDiscoveredCommands(typeof(NestedCommands).Assembly)
        );

        var cmd          = fixture.GetNestedBookRoom(new DateTime(2023, 10, 1));
        var streamEvents = await fixture.ExecuteRequest<NestedBookRoom, BookingState>(cmd, NestedBookRoute, cmd.BookingId);
        await VerifyJson(streamEvents);
    }
}
