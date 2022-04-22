using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

public delegate ValueTask<GatewayContext<TProduceOptions>?> RouteAndTransform<TProduceOptions>(
    IMessageConsumeContext message
);

public class GatewayHandler<TProduceOptions> : BaseEventHandler
    where TProduceOptions : class {
    readonly IEventProducer<TProduceOptions> _eventProducer;

    readonly RouteAndTransform<TProduceOptions> _transform;
    readonly bool                               _awaitProduce;

    public GatewayHandler(
        IEventProducer<TProduceOptions>    eventProducer,
        RouteAndTransform<TProduceOptions> transform,
        bool                               awaitProduce
    ) {
        _eventProducer = eventProducer;
        _transform     = transform;
        _awaitProduce  = awaitProduce;
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
        catch (OperationCanceledException e) {
            context.Nack<GatewayHandler>(e);
        }

        return EventHandlingStatus.Success;
    }
}
