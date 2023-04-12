// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Eventuous.Diagnostics;

using Metrics;
using static Tracing.Constants.Components;

public sealed class PersistenceMetrics : IWithCustomTags, IDisposable {
    const string Category = "persistence";

    public static readonly string MeterName = EventuousDiagnostics.GetMeterName(Category);

    public const string ListenerName = $"{DiagnosticName.BaseName}.{Category}";
    public const string OperationTag = "operation";

    readonly Meter                                     _meter;
    readonly MetricsListener<EventStoreMetricsContext> _listener;

    KeyValuePair<string, object?>[]? _customTags;

    public PersistenceMetrics() {
        _meter = EventuousDiagnostics.GetMeter(MeterName);

        var duration = _meter.CreateHistogram<double>(
            EventStore,
            "ms",
            "Event store operation duration, milliseconds"
        );

        var errorCount = _meter.CreateCounter<long>(
            $"{EventStore}.errors",
            "errors",
            "Number of failed event store operations"
        );

        _listener = new MetricsListener<EventStoreMetricsContext>(ListenerName, duration, errorCount, GetTags);

        TagList GetTags(EventStoreMetricsContext ctx)
            => new TagList(_customTags) { new(OperationTag, ctx.Operation) };
    }

    public void Dispose() {
        _listener.Dispose();
        _meter.Dispose();
    }

    public void SetCustomTags(TagList customTags)
        => _customTags = customTags.ToArray();
}

record EventStoreMetricsContext(string Operation);
