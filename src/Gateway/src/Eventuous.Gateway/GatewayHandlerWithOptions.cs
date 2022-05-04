using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

public delegate ValueTask<GatewayMessage<TProduceOptions>[]> RouteAndTransform<TProduceOptions>(
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
        var shovelMessages = await _transform(context).NoContext();

        if (shovelMessages.Length == 0) return EventHandlingStatus.Ignored;

        AcknowledgeProduce? onAck = null;

        if (context is DelayedAckConsumeContext delayed) {
            onAck = _ => delayed.Acknowledge();
        }

        try {
            var grouped = shovelMessages.GroupBy(x => x.TargetStream);

            await grouped
                .Select(x => ProduceToStream(x.Key, x))
                .WhenAll()
                .NoContext();
        }
        catch (OperationCanceledException e) {
            context.Nack<GatewayHandler>(e);
        }

        return _awaitProduce ? EventHandlingStatus.Success : EventHandlingStatus.Pending;

        Task ProduceToStream(StreamName streamName, IEnumerable<GatewayMessage<TProduceOptions>> toProduce)
            => toProduce.Select(
                    x =>
                        _eventProducer.Produce(
                            streamName,
                            x.Message,
                            x.GetMeta(context),
                            x.ProduceOptions,
                            onAck,
                            context.CancellationToken
                        )
                )
                .WhenAll();
    }
}
