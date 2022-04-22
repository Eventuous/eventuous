using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

public delegate ValueTask<GatewayContext?> RouteAndTransform(IMessageConsumeContext context);

class GatewayHandler : BaseEventHandler {
    readonly IEventProducer    _eventProducer;
    readonly RouteAndTransform _transform;
    readonly bool              _awaitProduce;

    public GatewayHandler(IEventProducer eventProducer, RouteAndTransform transform, bool awaitProduce) {
        _eventProducer = eventProducer;
        _transform     = transform;
        _awaitProduce  = awaitProduce;
    }

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        var shovelMessage = await _transform(context).NoContext();

        if (shovelMessage?.Message == null) return EventHandlingStatus.Ignored;

        await _eventProducer
            .Produce(
                shovelMessage.TargetStream,
                shovelMessage.Message,
                shovelMessage.GetMeta(context),
                context.CancellationToken
            )
            .NoContext();

        return _awaitProduce ? EventHandlingStatus.Success : EventHandlingStatus.Pending;
    }
}
