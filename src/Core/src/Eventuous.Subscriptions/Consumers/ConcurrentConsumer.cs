using Eventuous.Subscriptions.Channels;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers;

public sealed class ConcurrentConsumer : IMessageConsumer, IAsyncDisposable {
    readonly ConcurrentChannelWorker<DelayedAckConsumeContext> _worker;
    readonly IMessageConsumer                                  _inner;

    public ConcurrentConsumer(IMessageConsumer inner, int concurrencyLimit, int bufferSize = 10) {
        _inner = inner;

        _worker = new ConcurrentChannelWorker<DelayedAckConsumeContext>(
            Channel.CreateBounded<DelayedAckConsumeContext>(concurrencyLimit * bufferSize),
            ConsumeInt,
            concurrencyLimit
        );
    }

    async ValueTask ConsumeInt(DelayedAckConsumeContext ctx, CancellationToken cancellationToken) {
        await _inner.Consume(ctx, cancellationToken).NoContext();
        await ctx.Acknowledge(cancellationToken).NoContext();
    }

    public ValueTask Consume(IMessageConsumeContext context, CancellationToken cancellationToken) {
        if (context is not DelayedAckConsumeContext ctx) {
            throw new InvalidCastException("Round robin consumer only works with delayed acknowledgement");
        }

        return _worker.Write(ctx, cancellationToken);
    }

    public ValueTask DisposeAsync() => _worker.DisposeAsync();
}