// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Metrics;
using Eventuous.Diagnostics.Tracing;

namespace Eventuous.Subscriptions;

using Context;
using Diagnostics;

public class TracedEventHandler : IEventHandler {
    readonly DiagnosticSource _metricsSource = new DiagnosticListener(SubscriptionMetrics.ListenerName);

    public TracedEventHandler(IEventHandler eventHandler) {
        _innerHandler = eventHandler;

        _defaultTags = new[] {
            new KeyValuePair<string, object?>(
                TelemetryTags.Eventuous.EventHandler,
                eventHandler.GetType().Name
            )
        };

        DiagnosticName = _innerHandler.DiagnosticName;
    }

    readonly IEventHandler                   _innerHandler;
    readonly KeyValuePair<string, object?>[] _defaultTags;

    public string DiagnosticName { get; }

    public async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        using var activity = SubscriptionActivity
            .Create(
                $"{Constants.Components.EventHandler}.{DiagnosticName}/{context.MessageType}",
                ActivityKind.Internal,
                tags: _defaultTags
            )
            ?.SetContextTags(context)
            ?.Start();

        using var measure = Measure.Start(
            _metricsSource,
            new SubscriptionMetrics.SubscriptionMetricsContext(DiagnosticName, context)
        );

        try {
            var status = await _innerHandler.HandleEvent(context).NoContext();

            if (activity != null && status == EventHandlingStatus.Ignored) activity.ActivityTraceFlags = ActivityTraceFlags.None;

            activity?.SetActivityStatus(ActivityStatus.Ok());

            return status;
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            measure.SetError();
            throw;
        }
    }
}
