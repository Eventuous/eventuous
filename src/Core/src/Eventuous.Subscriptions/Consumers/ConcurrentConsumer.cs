using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Eventuous.Subscriptions.Channels;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers;

public sealed class ConcurrentConsumer : IMessageConsumer, IAsyncDisposable {
    readonly ConcurrentChannelWorker<DelayedAckConsumeContext> _worker;
    readonly IMessageConsumer                                  _inner;
    readonly Type                                              _innerType;

    public ConcurrentConsumer(
        IMessageConsumer eventHandler,
        int              concurrencyLimit,
        int              bufferSize = 10
    ) {
        _inner     = Ensure.NotNull(eventHandler, nameof(eventHandler));
        _innerType = _inner.GetType();

        _worker = new ConcurrentChannelWorker<DelayedAckConsumeContext>(
            Channel.CreateBounded<DelayedAckConsumeContext>(concurrencyLimit * bufferSize),
            DelayedConsume,
            concurrencyLimit
        );
    }

    async ValueTask DelayedConsume(
        DelayedAckConsumeContext ctx,
        CancellationToken        cancellationToken
    ) {
        using var activity = ctx.Items.TryGetItem<Activity>("activity")?.Start();

        try {
            await _inner.Consume(ctx, cancellationToken).NoContext();
            await ctx.Acknowledge(cancellationToken).NoContext();
        }
        catch (Exception e) {
            ctx.Nack(_innerType, e);
        }

        if (activity != null && ctx.WasIgnored())
            activity.ActivityTraceFlags = ActivityTraceFlags.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask Consume(IMessageConsumeContext context, CancellationToken cancellationToken) {
        if (context is not DelayedAckConsumeContext ctx) {
            throw new InvalidCastException(
                "Round robin consumer only works with delayed acknowledgement"
            );
        }

        return _worker.Write(ctx, cancellationToken);
    }

    public ValueTask DisposeAsync() => _worker.DisposeAsync();
}