using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

public delegate ValueTask<GatewayContext?> RouteAndTransform(IMessageConsumeContext context);

class GatewayHandler : BaseEventHandler {
    readonly IEventProducer    _eventProducer;
    readonly RouteAndTransform _transform;

    public GatewayHandler(
        IEventProducer    eventProducer,
        RouteAndTransform transform
    ) {
        _eventProducer = eventProducer;
        _transform     = transform;
    }

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        var shovelMessage = await _transform(context).NoContext();

        if (shovelMessage?.Message == null)
            return EventHandlingStatus.Ignored;

        await _eventProducer
            .Produce(
                shovelMessage.TargetStream,
                shovelMessage.Message,
                shovelMessage.GetMeta(context),
                context.CancellationToken
            )
            .NoContext();

        return EventHandlingStatus.Handled;
    }
}