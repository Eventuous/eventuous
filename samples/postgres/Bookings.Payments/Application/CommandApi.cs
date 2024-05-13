using Eventuous;
using Eventuous.AspNetCore.Web;
using Microsoft.AspNetCore.Mvc;

namespace Bookings.Payments.Application;

using Domain;
using static PaymentCommands;

[Route("payment")]
public class CommandApi(ICommandService<Payment, PaymentState> service) : CommandHttpApiBase<Payment, PaymentState>(service) {
    [HttpPost]
    public Task<ActionResult<Result<PaymentState>>> RegisterPayment([FromBody] RecordPayment cmd, CancellationToken cancellationToken)
        => Handle(cmd, cancellationToken);
}
