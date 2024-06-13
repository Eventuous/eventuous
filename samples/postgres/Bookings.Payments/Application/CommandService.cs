using System.Text.Json.Serialization;
using Bookings.Payments.Domain;
using Eventuous;
using Eventuous.AspNetCore.Web;

namespace Bookings.Payments.Application;

public class CommandService : CommandService<Payment, PaymentState, PaymentId> {
    public CommandService(IAggregateStore store) : base(store) {
        On<PaymentCommands.RecordPayment>()
            .InState(ExpectedState.New)
            .GetId(cmd => new PaymentId(cmd.PaymentId))
            .Act(ProcessPayment);

        return;

        void ProcessPayment(Payment payment, PaymentCommands.RecordPayment cmd)
            => payment.ProcessPayment(
                new PaymentId(cmd.PaymentId),
                cmd.BookingId,
                new Money(cmd.Amount, cmd.Currency),
                cmd.Method,
                cmd.Provider
            );
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
