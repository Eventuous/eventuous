using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
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
                $"{context.SubscriptionId}/{context.MessageType}",
                ActivityKind.Consumer,
                context,
                _defaultTags
            )
            : Activity.Current;

        if (activity?.IsAllDataRequested == true) {
            var partitionId = context.Items.TryGetItem<long>(ContextKeys.PartitionId);
            activity.SetContextTags(context)?.SetTag(TelemetryTags.Eventuous.Partition, partitionId);
        }

        try {
            await next(context).NoContext();

            if (activity != null && context.WasIgnored())
                activity.ActivityTraceFlags = ActivityTraceFlags.None;
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            throw;
        }
    }
}