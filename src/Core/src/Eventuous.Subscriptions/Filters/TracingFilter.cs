using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using ActivityStatus = Eventuous.Diagnostics.ActivityStatus;

namespace Eventuous.Subscriptions.Filters;

public class TracingFilter : ConsumeFilter {
    readonly KeyValuePair<string, object?>[] _defaultTags;

    public TracingFilter(params KeyValuePair<string, object?>[] tags) => _defaultTags = tags;

    public override async ValueTask Send(
        IMessageConsumeContext                   context,
        Func<IMessageConsumeContext, ValueTask>? next
    ) {
        if (context.Message == null || next == null) return;

        using var activity = Activity.Current?.Context != context.ParentContext
            ? SubscriptionActivity.Start(TracingConstants.ConsumerOperation, context, _defaultTags)
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