using Eventuous.Extensions.AspNetCore;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Eventuous.Sut.AspNetCore;

public class BookingApi(ICommandService<BookingState> service, CommandMap<HttpContext>? commandMap = null)
    : CommandHttpApiBase<BookingState>(service, commandMap) {
    [HttpPost("v2/pay")]
    [ProducesResult<BookingState>]
    [ProducesConflict]
    [ProducesDomainError]
    [ProducesNotFound]
    public async Task<IActionResult?> RegisterPayment([FromBody] RegisterPaymentHttp cmd, CancellationToken cancellationToken) {
        var result = await Handle<RegisterPaymentHttp, Commands.RecordPayment>(cmd, cancellationToken);

        return result.Result;
    }

    public record RegisterPaymentHttp(string BookingId, string PaymentId, float Amount, DateTimeOffset PaidAt);
}
