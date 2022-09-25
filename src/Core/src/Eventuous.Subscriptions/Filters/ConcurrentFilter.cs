// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Threading.Channels;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Channels;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Logging;

namespace Eventuous.Subscriptions.Filters;

public sealed class ConcurrentFilter : ConsumeFilter<DelayedAckConsumeContext>, IAsyncDisposable {
    readonly ConcurrentChannelWorker<WorkerTask> _worker;

    public ConcurrentFilter(uint concurrencyLimit, uint bufferSize = 10) {
        var capacity = (int)(concurrencyLimit * bufferSize);

        var options = new BoundedChannelOptions(capacity) {
            SingleReader = concurrencyLimit == 1, SingleWriter = true
        };

        _worker = new ConcurrentChannelWorker<WorkerTask>(
            Channel.CreateBounded<WorkerTask>(options),
            DelayedConsume,
            (int)concurrencyLimit
        );
    }

    static async ValueTask DelayedConsume(WorkerTask workerTask, CancellationToken ct) {
        var ctx = workerTask.Context;

        using var activity = ctx.Items.GetItem<Activity>(ContextItemKeys.Activity)?.Start();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken, ct);
        ctx.CancellationToken = cts.Token;
        Logger.Current        = ctx.LogContext;

        try {
            await workerTask.Next(ctx).NoContext();

            if (ctx.HasFailed()) {
                var exception = ctx.HandlingResults.GetException();

                switch (exception) {
                    case TaskCanceledException: break;
                    case null:                  throw new ApplicationException("Event handler failed");
                    default:                    throw exception;
                }
            }

            if (!ctx.HandlingResults.IsPending()) await ctx.Acknowledge().NoContext();
        }
        catch (TaskCanceledException) {
            ctx.Ignore<ConcurrentFilter>();
        }
        catch (Exception e) {
            ctx.LogContext.MessageHandlingFailed(nameof(ConcurrentFilter), workerTask.Context, e);
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
        // Logger.Configure(_subscriptionId, _loggerFactory);
        // Logger.Current.Info("Stopping the concurrent filter worker");
        return _worker.DisposeAsync();
    }
}
