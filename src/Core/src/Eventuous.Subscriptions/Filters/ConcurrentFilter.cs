using System.Diagnostics;
using System.Threading.Channels;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Channels;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Subscriptions.Filters;

public sealed class ConcurrentFilter : ConsumeFilter<DelayedAckConsumeContext>, IAsyncDisposable {
    readonly ConcurrentChannelWorker<WorkerTask> _worker;

    public ConcurrentFilter(uint concurrencyLimit, uint bufferSize = 10)
        => _worker = new ConcurrentChannelWorker<WorkerTask>(
            Channel.CreateBounded<WorkerTask>((int)(concurrencyLimit * bufferSize)),
            DelayedConsume,
            (int)concurrencyLimit
        );

    static async ValueTask DelayedConsume(WorkerTask workerTask, CancellationToken ct) {
        var ctx = workerTask.Context;

        using var activity = ctx.Items.TryGetItem<Activity>("activity")?.Start();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken, ct);
        ctx.CancellationToken = cts.Token;

        try {
            await workerTask.Next(ctx).NoContext();
            if (ctx.HasFailed()) throw ctx.HandlingResults.GetException();

            await ctx.Acknowledge().NoContext();
        }
        catch (Exception e) {
            Log.MessageHandlingFailed(nameof(ConcurrentFilter), workerTask.Context, e);
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            await ctx.Fail(e).NoContext();
        }

        if (activity != null && ctx.WasIgnored()) activity.ActivityTraceFlags = ActivityTraceFlags.None;
    }

    public override ValueTask Send(DelayedAckConsumeContext context, Func<DelayedAckConsumeContext, ValueTask>? next) {
        if (next == null) throw new InvalidOperationException("Concurrent context must have a next filer");

        return _worker.Write(new WorkerTask(context, next), context.CancellationToken);
    }

    record WorkerTask(DelayedAckConsumeContext Context, Func<DelayedAckConsumeContext, ValueTask> Next);

    public ValueTask DisposeAsync() {
        Log.Stopping(nameof(ConcurrentFilter), "worker", "");
        return _worker.DisposeAsync();
    }
}
