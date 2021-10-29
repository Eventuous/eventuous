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

    public void SetLogger(SubscriptionLog subscriptionLogger) => Log = subscriptionLogger;

    SubscriptionLog? Log { get; set; }

    public async Task HandleEvent(
        ReceivedEvent     evt,
        CancellationToken cancellationToken
    ) {
        var shovelMessage = await _transform(evt).NoContext();
        if (shovelMessage?.Message == null) return;

        await _eventProducer
            .Produce(
                shovelMessage.TargetStream,
                shovelMessage.Message,
                shovelMessage.GetMeta(evt),
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

    public void SetLogger(SubscriptionLog subscriptionLogger) => Log = subscriptionLogger;

    SubscriptionLog? Log { get; set; }

    public async Task HandleEvent(
        ReceivedEvent     evt,
        CancellationToken cancellationToken
    ) {
        var shovelMessage = await _transform(evt).NoContext();
        if (shovelMessage?.Message == null) return;

        await _eventProducer.Produce(
                shovelMessage.TargetStream,
                shovelMessage.Message,
                shovelMessage.GetMeta(evt),
                shovelMessage.ProduceOptions,
                cancellationToken
            )
            .NoContext();
    }
}

static class ShovelMetaHelper {
    public static Metadata GetMeta(this ShovelMessage shovelMessage, ReceivedEvent evt) {
        var (_, _, metadata) = shovelMessage;
        var meta = metadata == null ? new Metadata() : new Metadata(metadata);
        if (meta.GetMessageId() == evt.Metadata?.GetMessageId()) meta.WithMessageId(Guid.NewGuid());
        return meta.WithCausationId(evt.EventId);
    }
}