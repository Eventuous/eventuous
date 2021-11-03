using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Logging;

namespace Eventuous.Shovel;

class ShovelHandler<TProducer> : IEventHandler where TProducer : class, IEventProducer {
    readonly TProducer         _eventProducer;
    readonly RouteAndTransform _transform;

    public ShovelHandler(
        TProducer         eventProducer,
        RouteAndTransform transform
    ) {
        _eventProducer = eventProducer;
        _transform     = transform;
    }

    public async Task HandleEvent(
        IMessageConsumeContext context,
        CancellationToken     cancellationToken
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

class ShovelHandler<TProducer, TProduceOptions> : IEventHandler
    where TProducer : class, IEventProducer<TProduceOptions>
    where TProduceOptions : class {
    readonly TProducer _eventProducer;

    readonly RouteAndTransform<TProduceOptions> _transform;

    public ShovelHandler(
        TProducer                          eventProducer,
        RouteAndTransform<TProduceOptions> transform
    ) {
        _eventProducer = eventProducer;
        _transform     = transform;
    }

    public async Task HandleEvent(
        IMessageConsumeContext context,
        CancellationToken     cancellationToken
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
    public static Metadata GetMeta(this ShovelContext shovelContext, IMessageConsumeContext context) {
        var (_, _, metadata) = shovelContext;
        var meta = metadata == null ? new Metadata() : new Metadata(metadata);
        return meta.WithCausationId(context.EventId);
    }
}