// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics.Metrics;

namespace Eventuous.Diagnostics.Tracing;

public abstract class BaseTracer {
    protected static readonly DiagnosticSource MetricsSource = new DiagnosticListener(PersistenceMetrics.ListenerName);

    protected abstract string ComponentName { get; }

    static readonly KeyValuePair<string, object?>[] DefaultTags = EventuousDiagnostics.Tags
        .Concat([new(TelemetryTags.Db.System, "eventstore")])
        .ToArray();
    
    protected async Task<T> Trace<T>(StreamName stream, string operation, Func<Task<T>> task) {
        using var activity = StartActivity(stream, operation);
        using var measure  = Measure.Start(MetricsSource, new PersistenceMetricsContext(ComponentName, operation));

        try {
            var result = await task().NoContext();
            activity?.SetActivityStatus(ActivityStatus.Ok());

            return result;
        } catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            measure.SetError();

            throw;
        }
    }

    protected async Task Trace(StreamName stream, string operation, Func<Task> task) {
        using var activity = StartActivity(stream, operation);
        using var measure  = Measure.Start(MetricsSource, new PersistenceMetricsContext(ComponentName, operation));

        try {
            await task().NoContext();
            activity?.SetActivityStatus(ActivityStatus.Ok());
        } catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            measure.SetError();

            throw;
        }
    }

    protected static Activity? StartActivity(StreamName stream, string operationName) {
        if (!EventuousDiagnostics.Enabled) return null;

        var streamName = stream.ToString();

        var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
            $"{Constants.Components.EventStore}.{operationName}/{streamName}",
            ActivityKind.Server,
            parentContext: default,
            DefaultTags,
            idFormat: ActivityIdFormat.W3C
        );

        if (activity is { IsAllDataRequested: true }) {
            activity.SetTag(TelemetryTags.Db.Operation, operationName);
            activity.SetTag(TelemetryTags.Eventuous.Stream, streamName);
        }

        return activity?.Start();
    }
}
