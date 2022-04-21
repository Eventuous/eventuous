using Eventuous.AspNetCore.Web;
using Eventuous.Sut.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Eventuous.Tests.AspNetCore.Web;

public class HttpCommandTests {
    [Fact]
    public void RegisterAggregateCommands() {
        var builder = WebApplication.CreateBuilder();

        var app = builder.Build();

        var b = app.MapDiscoveredCommands<Booking>(typeof(BookRoom).Assembly);

        b.DataSources.First().Endpoints[0].DisplayName.Should().Be("HTTP: POST book");
    }

    [HttpCommand(Route = "book")]
    public record BookRoom(string BookingId, string RoomId, string GuestId);
}
