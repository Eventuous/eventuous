using Bookings.Payments.Domain;
using Eventuous;
using Eventuous.AspNetCore.Web;
using Microsoft.AspNetCore.Mvc;
using static Bookings.Payments.Application.PaymentCommands;

namespace Bookings.Payments.Application; 

[Route("payment")]
public class CommandApi : CommandHttpApiBase<Payment> {
    public CommandApi(IStateCommandService<Payment> service) : base(service) { }

    [HttpPost]
    public Task<ActionResult<Result<Payment>>> RegisterPayment([FromBody] RecordPayment cmd, CancellationToken cancellationToken)
        => Handle(cmd, cancellationToken);
}