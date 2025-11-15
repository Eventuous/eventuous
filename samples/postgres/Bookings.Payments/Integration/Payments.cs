using Bookings.Payments.Domain;
using Eventuous;
using Eventuous.Gateway;
using Eventuous.RabbitMq.Producers;
using Eventuous.Subscriptions.Context;
using static Bookings.Payments.Integration.IntegrationEvents;

namespace Bookings.Payments.Integration;

public static class PaymentsGateway {
    static readonly StreamName             Stream         = new("PaymentsIntegration");
    static readonly RabbitMqProduceOptions ProduceOptions = new();

    public static ValueTask<GatewayMessage<RabbitMqProduceOptions>[]> Transform(IMessageConsumeContext original) {
        var result = original.Message is PaymentEvents.PaymentRecorded evt
            ? new GatewayMessage<RabbitMqProduceOptions>(
                Stream,
                new BookingPaymentRecorded(original.Stream.GetId(), evt.BookingId, evt.Amount, evt.Currency),
                new(),
                ProduceOptions
            )
            : null;

        return ValueTask.FromResult<GatewayMessage<RabbitMqProduceOptions>[]>(result != null ? [result] : []);
    }
}

public static class IntegrationEvents {
    [EventType("BookingPaymentRecorded")]
    public record BookingPaymentRecorded(string PaymentId, string BookingId, float Amount, string Currency);
}
