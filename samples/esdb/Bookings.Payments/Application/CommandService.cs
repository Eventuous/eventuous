using System.Text.Json.Serialization;
using Bookings.Payments.Domain;
using Eventuous;
using Eventuous.Extensions.AspNetCore.Http;

namespace Bookings.Payments.Application;

public class CommandService : CommandService<PaymentState> {
    public CommandService(IEventStore store) : base(store) {
        On<PaymentCommands.RecordPayment>()
            .InState(ExpectedState.New)
            .GetStream(cmd => GetStream(cmd.PaymentId))
            .Act(cmd => [new PaymentEvents.PaymentRecorded(cmd.BookingId, cmd.Amount, cmd.Currency, cmd.Method, cmd.Provider)]);
    }
}

// [AggregateCommands(typeof(Payment))]
public static class PaymentCommands {
    [HttpCommand]
    public record RecordPayment(
            string                        PaymentId,
            string                        BookingId,
            float                         Amount,
            string                        Currency,
            string                        Method,
            string                        Provider,
            [property: JsonIgnore] string PaidBy
        );
}
