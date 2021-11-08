using System.Diagnostics;
using System.Threading.Channels;
using Eventuous.Subscriptions.Channels;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters;

public class ConcurrentFilter : ConsumeFilter<DelayedAckConsumeContext> {
    readonly ConcurrentChannelWorker<WorkerTask> _worker;

    public ConcurrentFilter(int concurrencyLimit, int bufferSize = 10) {
        _worker = new ConcurrentChannelWorker<WorkerTask>(
            Channel.CreateBounded<WorkerTask>(concurrencyLimit * bufferSize),
            DelayedConsume,
            concurrencyLimit
        );
    }

    static async ValueTask DelayedConsume(WorkerTask workerTask, CancellationToken ct) {
        var       ctx      = workerTask.Context;
        using var activity = ctx.Items.TryGetItem<Activity>("activity")?.Start();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken, ct);
        ctx.CancellationToken = cts.Token;

        try {
            await workerTask.Next(ctx).NoContext();
            await ctx.Acknowledge().NoContext();
        }
        catch (Exception e) {
            ctx.Nack<ConcurrentFilter>(e);
        }

        if (activity != null && ctx.WasIgnored())
            activity.ActivityTraceFlags = ActivityTraceFlags.None;
    }

    public override ValueTask Send(
        DelayedAckConsumeContext                   context,
        Func<DelayedAckConsumeContext, ValueTask>? next
    ) {
        if (next == null)
            throw new InvalidOperationException("Concurrent context must have a next filer");
        return _worker.Write(new WorkerTask(context, next), context.CancellationToken);
    }

    record WorkerTask(
        DelayedAckConsumeContext                  Context,
        Func<DelayedAckConsumeContext, ValueTask> Next
    );
}