// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics;

namespace Eventuous.Subscriptions.Diagnostics;

using Context;

static class SubscriptionActivity {
    public static Activity? Create(
            string                                      name,
            ActivityKind                                activityKind,
            IMessageConsumeContext                      context,
            IEnumerable<KeyValuePair<string, object?>>? tags = null
        ) {
        context.ParentContext = GetParentContext(context);
        var activity = Create(name, activityKind, context.ParentContext, tags);

        return activity?.SetContextTags(context);
    }

    public static Activity? Start(
            string                                      name,
            ActivityKind                                activityKind,
            IMessageConsumeContext                      context,
            IEnumerable<KeyValuePair<string, object?>>? tags = null
        )
        => Create(name, activityKind, context, tags)?.Start();

    public static Activity? SetContextTags(this Activity? activity, IMessageConsumeContext context) {
        if (activity is not { IsAllDataRequested: true }) return activity;

        return activity
            .SetTag(TelemetryTags.Message.Type, context.MessageType)
            .SetTag(TelemetryTags.Message.Id, context.MessageId)
            .SetTag(TelemetryTags.Messaging.MessageId, context.MessageId)
            .SetTag(TelemetryTags.Eventuous.Stream, context.Stream)
            .SetTag(TelemetryTags.Eventuous.Subscription, context.SubscriptionId)
            .CopyParentTag(TelemetryTags.Messaging.ConversationId)
            .SetOrCopyParentTag(TelemetryTags.Messaging.CorrelationId, context.Metadata?.GetCorrelationId());
    }

    static ActivityContext? GetParentContext(IBaseConsumeContext context) {
        if (Activity.Current != null) return Activity.Current.Context;

        if (context.Items.TryGetItem<Activity>(ContextItemKeys.Activity, out var parentActivity)) {
            return parentActivity?.Context;
        }

        var tracingData = context.Metadata?.GetTracingMeta();

        return tracingData?.ToActivityContext(true);
    }

    public static Activity? Create(
            string                                      name,
            ActivityKind                                activityKind,
            ActivityContext?                            parentContext = null,
            IEnumerable<KeyValuePair<string, object?>>? tags          = null
        )
        => EventuousDiagnostics.ActivitySource.CreateActivity(name, activityKind, parentContext ?? default, tags, idFormat: ActivityIdFormat.W3C);
}
