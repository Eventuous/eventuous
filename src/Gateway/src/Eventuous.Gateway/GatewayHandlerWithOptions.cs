// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

/// <summary>
/// Function that transforms one incoming message to zero or more outgoing messages.
/// </summary>
public delegate ValueTask<GatewayMessage<TProduceOptions>[]> RouteAndTransform<TProduceOptions>(IMessageConsumeContext message);

/// <inheritdoc />
class GatewayHandler<TProduceOptions> : BaseEventHandler where TProduceOptions : class {
    readonly IEventProducer<TProduceOptions>    _eventProducer;
    readonly RouteAndTransform<TProduceOptions> _transform;
    readonly bool                               _awaitProduce;

    public GatewayHandler(IEventProducer<TProduceOptions> eventProducer, RouteAndTransform<TProduceOptions> transform, bool awaitProduce) {
        _eventProducer = eventProducer;
        _transform     = transform;
        _awaitProduce  = awaitProduce;
    }

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        var shovelMessages = await _transform(context).NoContext();

        if (shovelMessages.Length == 0) return EventHandlingStatus.Ignored;

        AcknowledgeProduce? onAck = null;

        if (context is AsyncConsumeContext asyncContext) {
            onAck = _ => asyncContext.Acknowledge();
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
                            GatewayMetaHelper.GetContextMeta(context),
                            onAck,
                            context.CancellationToken
                        )
                )
                .WhenAll();
    }
}
