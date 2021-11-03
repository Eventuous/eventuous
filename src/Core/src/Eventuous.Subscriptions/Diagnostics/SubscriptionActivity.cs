using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Diagnostics;

public static class SubscriptionActivity {
    public static Activity? Start(
        IMessageConsumeContext                      context,
        IEnumerable<KeyValuePair<string, object?>>? tags = null
    ) {
        context.ParentContext ??= GetParentContext(context.Metadata);
        
        var activity  = CreateActivity(context.ParentContext, tags);
        if (activity == null) return activity;

        if (activity.IsAllDataRequested) {
            activity
                .SetTag(TelemetryTags.Message.Type, TypeMap.GetTypeName(context.Message!))
                .SetTag(TelemetryTags.Message.Id, context.EventId)
                .SetTag(TelemetryTags.Messaging.MessageId, context.EventId)
                .CopyParentTag(TelemetryTags.Messaging.ConversationId)
                .SetOrCopyParentTag(
                    TelemetryTags.Messaging.CorrelationId,
                    context.Metadata?.GetCorrelationId()
                );
        }

        return activity.Start();
    }

    static ActivityContext? GetParentContext(Metadata? metadata) {
        var tracingData   = metadata?.GetTracingMeta();
        return tracingData?.ToActivityContext(true);
    }

    static Activity? CreateActivity(
        ActivityContext?                            parentContext,
        IEnumerable<KeyValuePair<string, object?>>? tags
    )
        => EventuousDiagnostics.ActivitySource.CreateActivity(
            "consume",
            ActivityKind.Consumer,
            parentContext ?? default,
            tags,
            idFormat: ActivityIdFormat.W3C
        );
}