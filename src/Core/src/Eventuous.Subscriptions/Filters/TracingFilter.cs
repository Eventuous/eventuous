using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using ActivityStatus = Eventuous.Diagnostics.ActivityStatus;

namespace Eventuous.Subscriptions.Filters; 

public class TracingFilter : ConsumeFilter {
    readonly KeyValuePair<string, object?>[] _defaultTags;

    public TracingFilter(params KeyValuePair<string, object?>[] tags) => _defaultTags = tags;

    public override async ValueTask Send(IMessageConsumeContext context, Func<IMessageConsumeContext, ValueTask> next) {
        if (context.Message == null) return;
        
        using var activity = Activity.Current?.Context != context.ParentContext
            ? SubscriptionActivity.Start(context, _defaultTags) : Activity.Current;

        activity?.SetContextTags(context)?.Start();

        try {
            await next(context).NoContext();
        }
        catch (Exception e) {
            activity?.SetStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            throw;
        }
    }
}