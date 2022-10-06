// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Threading.Channels;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Channels;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Logging;

namespace Eventuous.Subscriptions.Filters;

public sealed class AsyncHandlingFilter : ConsumeFilter<AsyncConsumeContext>, IAsyncDisposable {
    readonly ConcurrentChannelWorker<WorkerTask> _worker;

    public AsyncHandlingFilter(uint concurrencyLimit, uint bufferSize = 10) {
        var capacity = (int)(concurrencyLimit * bufferSize);

        var options = new BoundedChannelOptions(capacity) {
            SingleReader = concurrencyLimit == 1, SingleWriter = true
        };

        _worker = new ConcurrentChannelWorker<WorkerTask>(
            Channel.CreateBounded<WorkerTask>(options),
            (task, token) => DelayedConsume(task, token),
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
            await workerTask.Filter.Value.Send(ctx, workerTask.Filter.Next).NoContext();

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
            ctx.Ignore<AsyncHandlingFilter>();
        }
        catch (Exception e) {
            ctx.LogContext.MessageHandlingFailed(nameof(AsyncHandlingFilter), workerTask.Context, e);
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            await ctx.Fail(e).NoContext();
        }

        if (activity != null && ctx.WasIgnored()) activity.ActivityTraceFlags = ActivityTraceFlags.None;
    }

    protected override ValueTask Send(AsyncConsumeContext context, LinkedListNode<IConsumeFilter>? next) {
        if (next == null) throw new InvalidOperationException("Concurrent context must have a next filer");

        return _worker.Write(new WorkerTask(context, next), context.CancellationToken);
    }

    record struct WorkerTask(AsyncConsumeContext Context, LinkedListNode<IConsumeFilter> Filter);

    public ValueTask DisposeAsync() {
        // Logger.Configure(_subscriptionId, _loggerFactory);
        // Logger.Current.Info("Stopping the concurrent filter worker");
        return _worker.DisposeAsync();
    }
}
