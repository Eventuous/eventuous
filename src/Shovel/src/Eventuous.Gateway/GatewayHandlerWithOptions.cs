using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

public delegate ValueTask<GatewayContext<TProduceOptions>?> RouteAndTransform<TProduceOptions>(
    IMessageConsumeContext message
);

public class GatewayHandler<TProduceOptions> : BaseEventHandler
    where TProduceOptions : class {
    readonly IEventProducer<TProduceOptions> _eventProducer;

    readonly RouteAndTransform<TProduceOptions> _transform;

    public GatewayHandler(IEventProducer<TProduceOptions> eventProducer, RouteAndTransform<TProduceOptions> transform) {
        _eventProducer = eventProducer;
        _transform     = transform;
    }

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        var shovelMessage = await _transform(context).NoContext();

        if (shovelMessage?.Message == null) return EventHandlingStatus.Ignored;

        try {
            await _eventProducer.Produce(
                    shovelMessage.TargetStream,
                    shovelMessage.Message,
                    shovelMessage.GetMeta(context),
                    shovelMessage.ProduceOptions,
                    context.CancellationToken
                )
                .NoContext();
        }
        catch (OperationCanceledException) {
            context.Nack<GatewayHandler>(null);
        }

        return EventHandlingStatus.Success;
    }
}
