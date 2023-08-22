// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Gateway;

/// <summary>
/// Function that transforms one incoming message to zero or more outgoing messages.
/// </summary>
public delegate ValueTask<GatewayMessage<TProduceOptions>[]> RouteAndTransform<TProduceOptions>(IMessageConsumeContext message);

/// <inheritdoc />
class GatewayHandler<TProduceOptions>(
        IEventProducer<TProduceOptions>    eventProducer,
        RouteAndTransform<TProduceOptions> transform,
        bool                               awaitProduce
    ) : BaseEventHandler
    where TProduceOptions : class {
    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        var shovelMessages = await transform(context).NoContext();

        if (shovelMessages.Length == 0) return EventHandlingStatus.Ignored;

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
            var grouped = shovelMessages.GroupBy(x => x.TargetStream);

            await grouped.Select(x => ProduceToStream(x.Key, x)).WhenAll().NoContext();
        } catch (OperationCanceledException e) { context.Nack<GatewayHandler<TProduceOptions>>(e); }

        return awaitProduce ? EventHandlingStatus.Success : EventHandlingStatus.Pending;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Task ProduceToStream(StreamName streamName, IEnumerable<GatewayMessage<TProduceOptions>> toProduce)
            => toProduce.Select(
                    x => eventProducer.Produce(
                        streamName,
                        x.Message,
                        x.GetMeta(context),
                        x.ProduceOptions,
                        GatewayMetaHelper.GetContextMeta(context),
                        onAck,
                        onFail,
                        context.CancellationToken
                    )
                )
                .WhenAll();
    }
}
