using System.Net;
using Eventuous.AspNetCore.Web;
using Eventuous.Sut.AspNetCore;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers;
using Eventuous.Tests.AspNetCore.Web.Fixture;
using NodaTime;
using RestSharp;
using BookRoom = Eventuous.Tests.AspNetCore.Web.Fixture.BookRoom;

namespace Eventuous.Tests.AspNetCore.Web;

public class HttpCommandTests : IDisposable {
    readonly TestEventListener _listener;

    public HttpCommandTests(ITestOutputHelper output) => _listener = new TestEventListener(output);

    [Fact]
    public void RegisterAggregateCommands() {
        var builder = WebApplication.CreateBuilder();

        using var app = builder.Build();

        var b = app.MapDiscoveredCommands<Booking>(typeof(BookRoom).Assembly);

        b.DataSources.First().Endpoints[0].DisplayName.Should().Be("HTTP: POST book");
    }

    [Fact]
    public async Task MapEnrichedCommand() {
        using var fixture = new ServerFixture();

        using var client = fixture.GetClient();

        var cmd = fixture.GetBookRoom();

        var response = await client.PostJsonAsync("/book", cmd);
        response.Should().Be(HttpStatusCode.OK);

        var storedEvents = await fixture.ReadStream<Booking>(cmd.BookingId);

        var actual = storedEvents.FirstOrDefault();
        actual.Should().NotBeNull();

        actual!.Payload.Should()
            .BeEquivalentTo(
                new BookingEvents.RoomBooked(
                    cmd.RoomId,
                    cmd.CheckIn,
                    cmd.CheckOut,
                    cmd.Price,
                    TestData.GuestId
                )
            );
    }

    public void Dispose() => _listener.Dispose();
}

[HttpCommand(Route = "book")]
record BookAnotherRoom(string BookingId, string RoomId, LocalDate CheckIn, LocalDate CheckOut, float Price);
