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
        var (parentContext, link) = GetParentOrLink(context);
        context.ParentContext     = parentContext;

        var activity = link == null
            ? Create(name, activityKind, parentContext, tags)
            : CreateLinkedRoot(name, activityKind, link.Value, tags);

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

    /// <summary>
    /// Decides how the consume span relates to what came before it: a parent, when the causality is real
    /// in-process causality inside a single consume, or a link, when the only thing available is the tracing
    /// context persisted in the message metadata.
    /// </summary>
    static (ActivityContext? ParentContext, ActivityLink? Link) GetParentOrLink(IBaseConsumeContext context) {
        // The current activity is only trusted as a parent when Eventuous created it: that's the
        // message's own pipeline activity (handler, then filters nested under it). A foreign ambient
        // activity — a test framework's per-test span, a client library's delivery span — must not
        // override the remote context propagated in the message metadata.
        if (Activity.Current?.Source.Name.StartsWith(EventuousDiagnostics.InstrumentationName, StringComparison.Ordinal) == true) {
            return (Activity.Current.Context, null);
        }

        if (context.Items.TryGetItem<Activity>(ContextItemKeys.Activity, out var parentActivity)) {
            return (parentActivity?.Context, null);
        }

        // The trace context restored from message metadata is durable data on the message, so it outlives the
        // process that produced it. Parenting to it would put every redelivery of that message — a replay, a
        // checkpoint reset, a hot resubscribe loop — into the same trace, forever: there is no live root to end
        // it and no sampling decision left to take, so the trace grows without any bound a collector can impose.
        // A link records the same causality without joining the trace, which is also what the OpenTelemetry
        // messaging conventions prescribe for an asynchronous "process" operation.
        if (context.Metadata?.GetTracingMeta().ToActivityContext(true) is { } remoteContext) {
            return (null, new ActivityLink(remoteContext));
        }

        return (Activity.Current?.Context, null);
    }

    static Activity? CreateLinkedRoot(
            string                                      name,
            ActivityKind                                activityKind,
            ActivityLink                                link,
            IEnumerable<KeyValuePair<string, object?>>? tags
        ) {
        // .NET offers no "explicitly parentless" argument: a default parent context makes Activity.Current the
        // parent. Suppressing it keeps an unrelated ambient span out of both the parentage and the sampling
        // decision, which has to be taken as a root. Assigning Activity.Current copies the execution context,
        // so it is only touched when there is actually something to suppress: this runs per consumed message.
        var ambient = Activity.Current;

        if (ambient != null) Activity.Current = null;

        try {
            var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
                name,
                activityKind,
                parentContext: default,
                tags,
                links: [link],
                idFormat: ActivityIdFormat.W3C
            );

            if (activity == null) return null;

            // .NET only materialises the trace id at creation if the sampler actually read it — a ratio-based
            // sampler does, an always-on one does not — and an unstarted activity reports an all-zero id
            // otherwise. Reuse the sampler's id when there is one, so the decision belongs to the span it was
            // taken for, and generate one when there isn't: pinning zeroes below would make the id permanent.
            var traceId = activity.TraceId;

            // Activity binds its parent from Activity.Current when it is started, and the async handling path
            // starts this activity further down the pipe, where something else may be ambient. Pinning the trace
            // id with a zero parent span id makes the span a root that a late Start cannot re-parent. The trace
            // flags are carried over, otherwise the span would silently stop being recorded.
            return activity.SetParentId(
                traceId == default ? ActivityTraceId.CreateRandom() : traceId,
                default,
                activity.ActivityTraceFlags
            );
        }
        finally {
            if (ambient != null) Activity.Current = ambient;
        }
    }

    public static Activity? Create(
            string                                      name,
            ActivityKind                                activityKind,
            ActivityContext?                            parentContext = null,
            IEnumerable<KeyValuePair<string, object?>>? tags          = null
        )
        => EventuousDiagnostics.ActivitySource.CreateActivity(name, activityKind, parentContext ?? default, tags, idFormat: ActivityIdFormat.W3C);
}
