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
        ReceivedEvent     evt,
        CancellationToken cancellationToken
    ) {
        var shovelMessage = await _transform(evt).NoContext();
        if (shovelMessage?.Message == null) return;

        await _eventProducer
            .Produce(
                shovelMessage.TargetStream,
                new[] { shovelMessage.Message },
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
        ReceivedEvent     evt,
        CancellationToken cancellationToken
    ) {
        var shovelMessage = await _transform(evt).NoContext();
        if (shovelMessage?.Message == null) return;

        await _eventProducer.Produce(
                shovelMessage.TargetStream,
                new[] { shovelMessage.Message },
                shovelMessage.ProduceOptions,
                cancellationToken
            )
            .NoContext();
    }
}