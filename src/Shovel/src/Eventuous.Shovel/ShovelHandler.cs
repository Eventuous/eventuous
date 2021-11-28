using Eventuous.Subscriptions.Context;

namespace Eventuous.Shovel;

class ShovelHandler : BaseEventHandler {
    readonly IEventProducer    _eventProducer;
    readonly RouteAndTransform _transform;

    public ShovelHandler(
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

class ShovelHandler<TProduceOptions> : BaseEventHandler
    where TProduceOptions : class {
    readonly IEventProducer<TProduceOptions> _eventProducer;

    readonly RouteAndTransform<TProduceOptions> _transform;

    public ShovelHandler(
        IEventProducer<TProduceOptions>    eventProducer,
        RouteAndTransform<TProduceOptions> transform
    ) {
        _eventProducer = eventProducer;
        _transform     = transform;
    }

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        var shovelMessage = await _transform(context).NoContext();

        if (shovelMessage?.Message == null)
            return EventHandlingStatus.Ignored;

        await _eventProducer.Produce(
                shovelMessage.TargetStream,
                shovelMessage.Message,
                shovelMessage.GetMeta(context),
                shovelMessage.ProduceOptions,
                context.CancellationToken
            )
            .NoContext();

        return EventHandlingStatus.Success;
    }
}

static class ShovelMetaHelper {
    public static Metadata GetMeta(
        this ShovelContext     shovelContext,
        IMessageConsumeContext context
    ) {
        var (_, _, metadata) = shovelContext;
        var meta = metadata == null ? new Metadata() : new Metadata(metadata);
        return meta.WithCausationId(context.MessageId);
    }
}