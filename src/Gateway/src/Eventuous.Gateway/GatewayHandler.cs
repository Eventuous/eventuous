// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

/// <summary>
/// Function that transforms one incoming message to zero or more outgoing messages.
/// </summary>
public delegate ValueTask<GatewayMessage<TProduceOptions>[]> RouteAndTransform<TProduceOptions>(IMessageConsumeContext message);

/// <inheritdoc />
class GatewayHandler<TProduceOptions>(
        IProducer<TProduceOptions>         producer,
        RouteAndTransform<TProduceOptions> transform,
        bool                               awaitProduce
    ) : BaseEventHandler
    where TProduceOptions : class {
    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        var transformedMessages = await transform(context).NoContext();

        if (transformedMessages.Length == 0) return EventHandlingStatus.Ignored;

        AcknowledgeProduce?  onAck  = null;
        ReportFailedProduce? onFail = null;

        if (!awaitProduce) {
            var asyncContext = context.GetContext<AsyncConsumeContext>();

            if (asyncContext != null) {
                onAck  = _ => asyncContext.Acknowledge();
                onFail = (_, error, ex) => asyncContext.Fail(ex ?? new ApplicationException(error));
            }
        }

        try {
            var contextMeta = GatewayMetaHelper.GetContextMeta(context);

            var requests = transformedMessages
                .GroupBy(x => (x.TargetStream, x.ProduceOptions))
                .Select(g => new ProduceRequest<TProduceOptions>(
                        g.Key.TargetStream,
                        g.Select(x => new ProducedMessage(x.Message, x.GetMeta(context), contextMeta) { OnAck = onAck, OnNack = onFail }),
                        g.Key.ProduceOptions
                    )
                )
                .ToArray();

            if (producer is GatewayProducer<TProduceOptions> gp)
                await gp.Produce(requests, context.CancellationToken).NoContext();
            else
                await Task.WhenAll(requests.Select(r => producer.Produce(r.Stream, r.Messages, r.Options, context.CancellationToken))).NoContext();
        } catch (OperationCanceledException e) { context.Nack<GatewayHandler<TProduceOptions>>(e); }

        return awaitProduce ? EventHandlingStatus.Success : EventHandlingStatus.Pending;
    }
}

class GatewayHandler<TTransform, TProduceOptions>(
        IProducer<TProduceOptions> producer,
        TTransform                 transform,
        bool                       awaitProduce
    ) : GatewayHandler<TProduceOptions>(producer, transform.RouteAndTransform, awaitProduce)
    where TProduceOptions : class
    where TTransform : class, IGatewayTransform<TProduceOptions>;
