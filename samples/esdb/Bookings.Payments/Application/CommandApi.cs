using Bookings.Payments.Domain;
using Eventuous;
using Eventuous.Extensions.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using static Bookings.Payments.Application.PaymentCommands;

namespace Bookings.Payments.Application; 

[Route("payment")]
public class CommandApi(ICommandService<PaymentState> service) : CommandHttpApiBase<PaymentState>(service) {
    [HttpPost]
    public Task<ActionResult<Result<PaymentState>.Ok>> RegisterPayment([FromBody] RecordPayment cmd, CancellationToken cancellationToken)
        => Handle(cmd, cancellationToken);
}