using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Logging;

namespace Eventuous.Shovel;

class ShovelHandler : IEventHandler {
    readonly IEventProducer    _eventProducer;
    readonly RouteAndTransform _transform;

    public ShovelHandler(
        IEventProducer    eventProducer,
        RouteAndTransform transform
    ) {
        _eventProducer = eventProducer;
        _transform     = transform;
    }

    public async Task HandleEvent(
        IMessageConsumeContext context,
        CancellationToken      cancellationToken
    ) {
        var shovelMessage = await _transform(context).NoContext();
        if (shovelMessage?.Message == null) return;

        await _eventProducer
            .Produce(
                shovelMessage.TargetStream,
                shovelMessage.Message,
                shovelMessage.GetMeta(context),
                cancellationToken
            )
            .NoContext();
    }
}

class ShovelHandler<TProduceOptions> : IEventHandler
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

    public async Task HandleEvent(
        IMessageConsumeContext context,
        CancellationToken      cancellationToken
    ) {
        var shovelMessage = await _transform(context).NoContext();
        if (shovelMessage?.Message == null) return;

        await _eventProducer.Produce(
                shovelMessage.TargetStream,
                shovelMessage.Message,
                shovelMessage.GetMeta(context),
                shovelMessage.ProduceOptions,
                cancellationToken
            )
            .NoContext();
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