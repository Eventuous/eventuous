using Eventuous.TestHelpers.TUnit;
using Microsoft.AspNetCore.Mvc.Testing;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.AspNetCore.BookingApi;

namespace Eventuous.Tests.Extensions.AspNetCore;

using Fixture;
using static SutBookingCommands;

[ClassDataSource<WebApplicationFactory<Program>>]
public class ControllerTests : IDisposable {
    readonly ServerFixture     _fixture;
    readonly TestEventListener _listener;

    public ControllerTests(WebApplicationFactory<Program> factory) {
        var commandMap = new CommandMap<HttpContext>()
            .Add<RegisterPaymentHttp, RecordPayment>(
                (x, ctx) => new(new(x.BookingId), x.PaymentId, new(x.Amount), x.PaidAt) { PaidBy = ctx.User.Identity?.Name }
            );

        _fixture = new(
            factory,
            services => {
                services.AddSingleton(commandMap);
                services.AddControllers();
            },
            app => {
                app.MapControllers();
                app.MapCommands<BookingState>().MapCommand<BookRoom>();
            }
        );

        _listener = new();
    }

    [Test]
    public async Task RecordPaymentUsingMappedCommand(CancellationToken cancellationToken) {
        using var client = _fixture.GetClient();

        var bookRoom = _fixture.GetBookRoom();

        await client.PostJsonAsync("/book", bookRoom, cancellationToken: cancellationToken);

        var registerPayment = new RegisterPaymentHttp(bookRoom.BookingId, bookRoom.RoomId, 100, DateTimeOffset.Now);

        var request  = new RestRequest("/v2/pay").AddJsonBody(registerPayment);
        var response = await client.ExecutePostAsync<Result<BookingState>.Ok>(request, cancellationToken: cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var expected = new BookingEvents.BookingFullyPaid(registerPayment.PaidAt);

        var events = await _fixture.ReadStream<Booking>(bookRoom.BookingId);
        var last   = events.LastOrDefault();
        last.Payload.Should().BeEquivalentTo(expected);
    }

    public void Dispose() => _listener.Dispose();
}
