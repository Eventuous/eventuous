using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Diagnostics;

public static class SubscriptionActivity {
    public static Activity? Create(
        IMessageConsumeContext                      context,
        IEnumerable<KeyValuePair<string, object?>>? tags = null
    ) {
        context.ParentContext ??= GetParentContext(context.Metadata);

        var activity = CreateActivity(context.ParentContext, tags);

        return activity?.SetContextTags(context);
    }

    public static Activity? Start(
        IMessageConsumeContext                      context,
        IEnumerable<KeyValuePair<string, object?>>? tags = null
    )
        => Create(context, tags)?.Start();

    public static Activity? SetContextTags(this Activity? activity, IMessageConsumeContext context) {
        if (activity is not { IsAllDataRequested: true }) return activity;

        return activity
            .SetTag(TelemetryTags.Message.Type, context.MessageType)
            .SetTag(TelemetryTags.Message.Id, context.MessageId)
            .SetTag(TelemetryTags.Messaging.MessageId, context.MessageId)
            .SetTag(TelemetryTags.Eventuous.Stream, context.Stream)
            .CopyParentTag(TelemetryTags.Messaging.ConversationId)
            .SetOrCopyParentTag(
                TelemetryTags.Messaging.CorrelationId,
                context.Metadata?.GetCorrelationId()
            );
    }

    static ActivityContext? GetParentContext(Metadata? metadata) {
        var tracingData = metadata?.GetTracingMeta();
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