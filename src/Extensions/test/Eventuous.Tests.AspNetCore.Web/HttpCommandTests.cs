namespace Eventuous.Tests.AspNetCore.Web;

using TestHelpers;
using Fixture;

public class HttpCommandTests(ITestOutputHelper output) : IDisposable {
    readonly TestEventListener _listener = new(output);

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
    public async Task MapDiscoveredCommand() {
        using var fixture = new ServerFixture(
            output,
            _ => { },
            app => app.MapDiscoveredCommands(typeof(NestedCommands).Assembly)
        );

        using var client = fixture.GetClient();

        var cmd = fixture.GetNestedBookRoom();

        var response = await client.PostJsonAsync("/nested-book", cmd);
        response.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapEnrichedCommand() {
        using var fixture = new ServerFixture(
            output,
            _ => { },
            app => app.MapAggregateCommands<Booking>().MapCommand<BookRoom>((x, _) => x with { GuestId = TestData.GuestId })
        );

        using var client = fixture.GetClient();

        var cmd = fixture.GetBookRoom();

        var response = await client.PostJsonAsync("/book", cmd);
        response.Should().Be(HttpStatusCode.OK);

        var storedEvents = await fixture.ReadStream<Booking>(cmd.BookingId);

        var actual = storedEvents.FirstOrDefault();

        actual.Payload
            .Should()
            .BeEquivalentTo(new BookingEvents.RoomBooked(cmd.RoomId, cmd.CheckIn, cmd.CheckOut, cmd.Price, TestData.GuestId));
    }

    public void Dispose()
        => _listener.Dispose();
}

[HttpCommand(Route = "book")]
record BookAnotherRoom(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price) {
    public string? GuestId { get; init; }
}
