using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;

namespace Eventuous.Subscriptions;

public class TracedEventHandler : IEventHandler {
    public TracedEventHandler(IEventHandler eventHandler) {
        _inner     = eventHandler;

        _defaultTags = new[] {
            new KeyValuePair<string, object?>(
                TelemetryTags.Eventuous.EventHandler,
                eventHandler.GetType().Name
            )
        };

        DiagnosticName = _inner.DiagnosticName;
    }

    readonly IEventHandler                   _inner;
    readonly KeyValuePair<string, object?>[] _defaultTags;

    public string DiagnosticName { get; }

    public async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        using var activity = SubscriptionActivity.Create(tags: _defaultTags)?.SetContextTags(context)?.Start();

        try {
            var status = await _inner.HandleEvent(context).NoContext();

            if (activity != null && status == EventHandlingStatus.Ignored)
                activity.ActivityTraceFlags = ActivityTraceFlags.None;

            return status;
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            throw;
        }
    }
}