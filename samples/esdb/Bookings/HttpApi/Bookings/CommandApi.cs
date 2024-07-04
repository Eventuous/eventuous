using Bookings.Domain.Bookings;
using Eventuous;
using Eventuous.Extensions.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using static Bookings.Application.BookingCommands;

namespace Bookings.HttpApi.Bookings;

/// <summary>
/// This controller exposes a Web API to execute HTTP POST requests matching application commands using the
/// command service registered in the DI container.
/// </summary>
/// <param name="service"></param>
[Route("/booking")]
public class CommandApi(ICommandService<BookingState> service) : CommandHttpApiBase<BookingState>(service) {
    [HttpPost]
    [Route("book")]
    public Task<ActionResult<Result<BookingState>.Ok>> BookRoom([FromBody] BookRoom cmd, CancellationToken cancellationToken)
        => Handle(cmd, cancellationToken);

    /// <summary>
    /// This endpoint is for demo purposes only. The normal flow to register booking payments is to submit
    /// a command via the Booking.Payments HTTP API. It then gets propagated to the Booking aggregate
    /// via the integration messaging flow.
    /// </summary>
    /// <param name="cmd">Command to register the payment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    [HttpPost]
    [Route("recordPayment")]
    public Task<ActionResult<Result<BookingState>.Ok>> RecordPayment([FromBody] RecordPayment cmd, CancellationToken cancellationToken)
        => Handle(cmd, cancellationToken);
}
