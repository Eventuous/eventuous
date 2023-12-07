using Bookings.Domain.Bookings;
using Eventuous;
using Eventuous.AspNetCore.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static Bookings.Application.BookingCommands;

namespace Bookings.HttpApi.Bookings;

[Route("/booking")]
public class CommandApi : CommandHttpApiBase<Booking> {
    public CommandApi(ICommandService<Booking> service) : base(service) { }

    [HttpPost]
    [Route("book")]
    public Task<ActionResult<Result>> BookRoom([FromBody] BookRoom cmd, CancellationToken cancellationToken)
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
    public Task<ActionResult<Result>> RecordPayment(
        [FromBody] RecordPayment cmd, CancellationToken cancellationToken
    )
        => Handle(cmd, cancellationToken);
}
