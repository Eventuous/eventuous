using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;

namespace Eventuous.Subscriptions;

public class TracedEventHandler : IEventHandler {
    public TracedEventHandler(IEventHandler eventHandler) {
        _inner     = eventHandler;
        _innerType = _inner.GetType();

        _defaultTags = new[] {
            new KeyValuePair<string, object?>(
                TelemetryTags.Eventuous.EventHandler,
                eventHandler.GetType().Name
            )
        };
    }

    readonly Type                            _innerType;
    readonly IEventHandler                   _inner;
    readonly KeyValuePair<string, object?>[] _defaultTags;

    public async ValueTask HandleEvent(IMessageConsumeContext context) {
        using var activity = Activity.Current?.Context != context.ParentContext
            ? SubscriptionActivity.Start(context, _defaultTags) : Activity.Current;

        activity?.SetContextTags(context)?.Start();

        try {
            await _inner.HandleEvent(context).NoContext();

            if (context.WasIgnored() && activity != null)
                activity.ActivityTraceFlags = ActivityTraceFlags.None;
        }
        catch (Exception e) {
            activity?.SetStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            context.Nack(_innerType, e);
        }
    }
}