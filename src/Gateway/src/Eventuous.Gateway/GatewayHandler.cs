// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

/// <summary>
/// Function that transforms one incoming message to zero or more outgoing messages.
/// </summary>
public delegate ValueTask<GatewayMessage[]> RouteAndTransform(IMessageConsumeContext context);

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
        var shovelMessages = await _transform(context).NoContext();

        if (shovelMessages.Length == 0) return EventHandlingStatus.Ignored;

        AcknowledgeProduce?  onAck  = null;
        ReportFailedProduce? onFail = null;

        if (_awaitProduce) {
            var asyncContext = context.GetContext<AsyncConsumeContext>();

            if (asyncContext != null) {
                onAck  = _ => asyncContext.Acknowledge();
                onFail = (_, error, ex) => asyncContext.Fail(ex ?? new ApplicationException(error));
            }
        }

        var grouped = shovelMessages.GroupBy(x => x.TargetStream);

        try {
            await grouped.Select(x => ProduceToStream(x.Key, x)).WhenAll();
        }
        catch (OperationCanceledException e) {
            context.Nack<GatewayHandler>(e);
        }

        return _awaitProduce ? EventHandlingStatus.Success : EventHandlingStatus.Pending;

        Task ProduceToStream(StreamName streamName, IEnumerable<GatewayMessage> toProduce) {
            var messages = toProduce
                .Select(
                    x => new ProducedMessage(x.Message, x.GetMeta(context), GatewayMetaHelper.GetContextMeta(context))
                        { OnAck = onAck, OnNack = onFail }
                );

            return _eventProducer.Produce(streamName, messages, context.CancellationToken);
        }
    }
}
