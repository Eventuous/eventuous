using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Eventuous.Subscriptions.Channels;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Consumers;

public sealed class ConcurrentConsumer : MessageConsumer, IAsyncDisposable {
    readonly ConcurrentChannelWorker<DelayedAckConsumeContext> _worker;
    readonly MessageConsumer                                  _inner;
    readonly Type                                              _innerType;

    public ConcurrentConsumer(
        MessageConsumer eventHandler,
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

    async ValueTask DelayedConsume(DelayedAckConsumeContext ctx, CancellationToken ct) {
        using var activity = ctx.Items.TryGetItem<Activity>("activity")?.Start();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken, ct);
        ctx.CancellationToken = cts.Token;

        try {
            await _inner.Consume(ctx).NoContext();
            await ctx.Acknowledge().NoContext();
        }
        catch (Exception e) {
            ctx.Nack(_innerType, e);
        }

        if (activity != null && ctx.WasIgnored())
            activity.ActivityTraceFlags = ActivityTraceFlags.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask Consume(IMessageConsumeContext context) {
        if (context is not DelayedAckConsumeContext ctx) {
            throw new InvalidCastException(
                "Round robin consumer only works with delayed acknowledgement"
            );
        }

        return _worker.Write(ctx, ctx.CancellationToken);
    }

    public ValueTask DisposeAsync() => _worker.DisposeAsync();
}