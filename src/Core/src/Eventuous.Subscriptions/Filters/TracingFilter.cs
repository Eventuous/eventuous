// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Tracing;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Tools;
using ActivityStatus = Eventuous.Diagnostics.ActivityStatus;

namespace Eventuous.Subscriptions.Filters;

public class TracingFilter : ConsumeFilter {
    readonly KeyValuePair<string, object?>[] _defaultTags;

    public TracingFilter(string consumerName) {
        var tags = new KeyValuePair<string, object?>[] { new(TelemetryTags.Eventuous.Consumer, consumerName) };

        _defaultTags = tags.Concat(EventuousDiagnostics.Tags).ToArray();
    }

    public override async ValueTask Send(
        IMessageConsumeContext                   context,
        Func<IMessageConsumeContext, ValueTask>? next
    ) {
        if (context.Message == null || next == null) return;

        using var activity = Activity.Current?.Context != context.ParentContext
            ? SubscriptionActivity.Start(
                $"{Constants.Components.Consumer}.{context.SubscriptionId}/{context.MessageType}",
                ActivityKind.Consumer,
                context,
                _defaultTags
            )
            : Activity.Current;

        if (activity?.IsAllDataRequested == true && context is DelayedAckConsumeContext delayedAckContext) {
            activity.SetContextTags(context)?.SetTag(TelemetryTags.Eventuous.Partition, delayedAckContext.PartitionId);
        }

        try {
            await next(context).NoContext();

            if (activity != null) {
                if (context.WasIgnored()) {
                    activity.ActivityTraceFlags = ActivityTraceFlags.None;
                }

                activity.SetActivityStatus(ActivityStatus.Ok());
            }
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            throw;
        }
    }
}
