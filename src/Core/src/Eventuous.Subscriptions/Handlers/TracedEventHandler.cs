// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Metrics;
using Eventuous.Diagnostics.Tracing;

namespace Eventuous.Subscriptions;

using Context;
using Diagnostics;

public class TracedEventHandler(IEventHandler eventHandler) : IEventHandler {
    // One listener for all handlers: the metrics listener only observes the latest listener announced under a name.
    static readonly DiagnosticSource MetricsSource = new DiagnosticListener(SubscriptionMetrics.ListenerName);

    readonly KeyValuePair<string, object?>[] _defaultTags = [new (TelemetryTags.Eventuous.EventHandler, eventHandler.GetType().Name)];

    public string DiagnosticName { get; } = eventHandler.DiagnosticName;

    readonly string                               _activityNamePrefix = $"{Constants.Components.EventHandler}.{eventHandler.DiagnosticName}/";
    readonly ConcurrentDictionary<string, string> _activityNames      = new();

    public async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        using var activity = SubscriptionActivity
            .Create(GetActivityName(context.MessageType), ActivityKind.Internal, tags: _defaultTags)
            ?.SetContextTags(context)
            ?.Start();

        // Nobody listening means nothing to record, so skip the measure and its context rather than allocate both.
        using var measure = MetricsSource.IsEnabled(Measure.EventName)
            ? Measure.Start(MetricsSource, new SubscriptionMetrics.SubscriptionMetricsContext(DiagnosticName, context))
            : null;

        try {
            var status = await eventHandler.HandleEvent(context).NoContext();

            if (activity != null && status == EventHandlingStatus.Ignored) activity.ActivityTraceFlags = ActivityTraceFlags.None;

            activity?.SetActivityStatus(ActivityStatus.Ok());

            return status;
        } catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested) {
            return EventHandlingStatus.Pending;
        } catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            measure?.SetError();

            throw;
        }
    }

    // Keyed defensively: a transport can still hand over a null type, which the dictionary would reject.
    string GetActivityName(string? messageType)
        => _activityNames.GetOrAdd(messageType ?? "", static (type, prefix) => prefix + type, _activityNamePrefix);
}
