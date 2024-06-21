using Microsoft.AspNetCore.Mvc.Testing;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.AspNetCore.BookingApi;

namespace Eventuous.Tests.AspNetCore.Web;

using TestHelpers;
using Fixture;
using static SutBookingCommands;

public class ControllerTests : IDisposable, IClassFixture<WebApplicationFactory<Program>> {
    readonly ServerFixture     _fixture;
    readonly TestEventListener _listener;

    public ControllerTests(WebApplicationFactory<Program> factory, ITestOutputHelper output) {
        var commandMap = new MessageMap()
            .Add<RegisterPaymentHttp, RecordPayment>(x => new(new(x.BookingId), x.PaymentId, new(x.Amount), x.PaidAt));

        _fixture = new(
            factory,
            output,
            services => {
                services.AddSingleton(commandMap);
                services.AddControllers();
            },
            app => {
                app.MapControllers();

                app
                    .MapCommands<BookingState>()
                    .MapCommand<BookRoom>();
            }
        );

        _listener = new(output);
    }

    [Fact]
    public async Task RecordPaymentUsingMappedCommand() {
        using var client = _fixture.GetClient();

        var bookRoom = _fixture.GetBookRoom();

        await client.PostJsonAsync("/book", bookRoom);

        var registerPayment = new RegisterPaymentHttp(bookRoom.BookingId, bookRoom.RoomId, 100, DateTimeOffset.Now);

        var request  = new RestRequest("/v2/pay").AddJsonBody(registerPayment);
        var response = await client.ExecutePostAsync<OkResult<BookingState>>(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var expected = new BookingEvents.BookingFullyPaid(registerPayment.PaidAt);

        var events = await _fixture.ReadStream<Booking>(bookRoom.BookingId);
        var last   = events.LastOrDefault();
        last.Payload.Should().BeEquivalentTo(expected);
    }

    public void Dispose() => _listener.Dispose();
}
