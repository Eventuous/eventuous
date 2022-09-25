// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Tracing;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Tools;

namespace Eventuous.Subscriptions;

public class TracedEventHandler : IEventHandler {
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

        try {
            var status = await _innerHandler.HandleEvent(context).NoContext();

            if (activity != null && status == EventHandlingStatus.Ignored)
                activity.ActivityTraceFlags = ActivityTraceFlags.None;

            activity?.SetActivityStatus(ActivityStatus.Ok());

            return status;
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e, $"Error handling {context.MessageType}"));
            throw;
        }
    }
}