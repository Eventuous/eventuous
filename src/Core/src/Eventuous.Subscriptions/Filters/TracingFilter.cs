// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics;
using Constants = Eventuous.Diagnostics.Tracing.Constants;

namespace Eventuous.Subscriptions.Filters;

using Context;
using Diagnostics;

public class TracingFilter : ConsumeFilter<IMessageConsumeContext> {
    readonly KeyValuePair<string, object?>[] _defaultTags;

    public TracingFilter(string consumerName) {
        var tags = new KeyValuePair<string, object?>[] { new(TelemetryTags.Eventuous.Consumer, consumerName) };

        _defaultTags = [.. tags, .. EventuousDiagnostics.Tags];
    }

    protected override async ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next) {
        if (context.Message == null || next == null) return;

        // The subscription's own activity is reused, not owned: disposing it would stop it before the
        // subscription is done with it, so only an activity started here gets disposed.
        var reuseCurrent = Activity.Current?.Context == context.ParentContext;

        using var started = reuseCurrent
            ? null
            : SubscriptionActivity.Start(
                $"{Constants.Components.Consumer}.{context.SubscriptionId}/{context.MessageType}",
                ActivityKind.Consumer,
                context,
                _defaultTags
            );

        var activity = reuseCurrent ? Activity.Current : started;

        if (activity?.IsAllDataRequested == true && context is AsyncConsumeContext asyncConsumeContext) {
            activity.SetContextTags(context)?.SetTag(TelemetryTags.Eventuous.Partition, asyncConsumeContext.PartitionId);
        }

        try {
            await next.Value.Send(context, next.Next).NoContext();

            if (activity != null) {
                if (context.WasIgnored()) {
                    activity.ActivityTraceFlags = ActivityTraceFlags.None;
                }

                // A handler failure is recorded with Nack, not thrown, and Nack has already set the error status
                if (!context.HasFailed()) activity.SetActivityStatus(ActivityStatus.Ok());
            }
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            throw;
        }
    }
}
