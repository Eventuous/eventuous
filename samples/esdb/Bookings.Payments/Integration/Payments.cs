using Bookings.Payments.Domain;
using Eventuous;
using Eventuous.KurrentDB.Producers;
using Eventuous.Gateway;
using Eventuous.Subscriptions.Context;
using static Bookings.Payments.Integration.IntegrationEvents;
// ReSharper disable NotAccessedPositionalProperty.Global

namespace Bookings.Payments.Integration;

public static class PaymentsGateway {
    static readonly StreamName Stream = new("PaymentsIntegration");

    public static ValueTask<GatewayMessage<KurrentDBProduceOptions>[]> Transform(IMessageConsumeContext original) {
        var result = original.Message is PaymentEvents.PaymentRecorded evt
            ? new GatewayMessage<KurrentDBProduceOptions>(
                Stream,
                new BookingPaymentRecorded(original.Stream.GetId(), evt.BookingId, evt.Amount, evt.Currency),
                new(),
                new()
            )
            : null;
        GatewayMessage<KurrentDBProduceOptions>[] gatewayMessages = result != null ? [result] : [];
        return ValueTask.FromResult(gatewayMessages);
    }
}

public static class IntegrationEvents {
    [EventType("BookingPaymentRecorded")]
    public record BookingPaymentRecorded(string PaymentId, string BookingId, float Amount, string Currency);
}