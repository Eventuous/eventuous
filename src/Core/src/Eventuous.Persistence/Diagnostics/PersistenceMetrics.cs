// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Eventuous.Diagnostics;

using Metrics;

public sealed class PersistenceMetrics : IWithCustomTags, IDisposable {
    public static readonly string MeterName = EventuousDiagnostics.GetMeterName(Category);

    const string Category     = "persistence";
    const string MetricPrefix = DiagnosticName.BaseName;

    public const string ListenerName       = $"{MetricPrefix}.{Category}";
    public const string ProcessingRateName = $"{MetricPrefix}.{Category}.duration";
    public const string ErrorCountName     = $"{MetricPrefix}.{Category}.errors.count";

    const string OperationTag = "operation";
    const string ComponentTag = "component";

    readonly Meter                                      _meter;
    readonly MetricsListener<PersistenceMetricsContext> _listener;

    KeyValuePair<string, object?>[]? _customTags;

    public PersistenceMetrics() {
        _meter = EventuousDiagnostics.GetMeter(MeterName);
        var duration   = _meter.CreateHistogram<double>(ProcessingRateName, "ms", "Event store operation duration, milliseconds");
        var errorCount = _meter.CreateCounter<long>(ErrorCountName, "errors", "Number of failed event store operations");
        _listener = new(ListenerName, duration, errorCount, GetTags);

        return;

        TagList GetTags(PersistenceMetricsContext ctx) => new(_customTags) { new(OperationTag, ctx.Operation), new(ComponentTag, ctx.Component) };
    }

    public void Dispose() {
        _listener.Dispose();
        _meter.Dispose();
    }

    public void SetCustomTags(TagList customTags) => _customTags = customTags.ToArray();
}

record PersistenceMetricsContext(string Component, string Operation);
