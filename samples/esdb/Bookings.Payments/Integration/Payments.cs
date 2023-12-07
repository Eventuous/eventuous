using Bookings.Payments.Domain;
using Eventuous;
using Eventuous.EventStore.Producers;
using Eventuous.Gateway;
using Eventuous.Subscriptions.Context;
using static Bookings.Payments.Integration.IntegrationEvents;

namespace Bookings.Payments.Integration;

public static class PaymentsGateway {
    static readonly StreamName Stream = new("PaymentsIntegration");
    
    public static ValueTask<GatewayMessage<EventStoreProduceOptions>[]> Transform(IMessageConsumeContext original) {
        GatewayMessage<EventStoreProduceOptions>[] result = original.Message is PaymentEvents.PaymentRecorded evt
            ? [new GatewayMessage<EventStoreProduceOptions>(
                Stream,
                new BookingPaymentRecorded(evt.PaymentId, evt.BookingId, evt.Amount, evt.Currency),
                new Metadata(),
                new EventStoreProduceOptions()
            )]
            : [];
        return ValueTask.FromResult(result);
    }
}

public static class IntegrationEvents {
    [EventType("BookingPaymentRecorded")]
    public record BookingPaymentRecorded(string PaymentId, string BookingId, float Amount, string Currency);
}