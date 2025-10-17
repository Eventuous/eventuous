using Microsoft.AspNetCore.Mvc.Testing;
using static Eventuous.Sut.AspNetCore.SutBookingCommands.NestedCommands;

namespace Eventuous.Tests.Extensions.AspNetCore;

using static SutBookingCommands;
using Fixture;

[ClassDataSource<WebApplicationFactory<Program>>]
public class DiscoveredCommandsTests(WebApplicationFactory<Program> factory) : TestBaseWithLogs {
    [Test]
    public async Task RegisterStateCommands() {
        var builder = WebApplication.CreateBuilder();

        await using var app = builder.Build();

        var b = app.MapDiscoveredCommands<BookingState>();

        var actual   = b.DataSources.First().Endpoints.Select(x => x.DisplayName).Order().ToList();
        var expected = new[] { "HTTP: POST nested-book", "HTTP: POST import2" };

        await Assert.That(actual).IsEquivalentTo(expected.Order());
    }

    [Test]
    public async Task RegisterStatesCommands() {
        var builder = WebApplication.CreateBuilder();

        await using var app = builder.Build();

        var b = app.MapDiscoveredCommands(typeof(TestCommands.DuplicateCommand));

        var actual   = b.DataSources.First().Endpoints.Select(x => x.DisplayName).Order().ToList();
        var expected = new[] { "HTTP: POST nested-book", "HTTP: POST import2", "HTTP: POST import-wrong" };

        await Assert.That(actual).IsEquivalentTo(expected.Order());
    }

    [Test]
    public async Task CallDiscoveredCommandRoute() {
        var fixture = new ServerFixture(
            factory,
            _ => { },
            app => app.MapDiscoveredCommands(typeof(TestCommands.ImportBookingHttp3))
        );

        var cmd          = ServerFixture.GetNestedBookRoom(new DateTime(2023, 10, 1));
        var streamEvents = await fixture.ExecuteRequest<NestedBookRoom, BookingState>(cmd, NestedBookRoute, cmd.BookingId);
        await VerifyJson(streamEvents);
    }
}
