// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics.Metrics;

namespace Eventuous.Diagnostics;

using static Tracing.Constants.Components;

public sealed class CommandServiceMetrics : IWithCustomTags, IDisposable {
    public static readonly string MeterName = EventuousDiagnostics.GetMeterName(Category);

    const string MetricPrefix = DiagnosticName.BaseName;
    const string Category     = "application";

    public const string ListenerName       = $"{MetricPrefix}.{Category}";
    public const string ProcessingRateName = $"{MetricPrefix}.{CommandService}.duration";
    public const string ErrorCountName     = $"{MetricPrefix}.{CommandService}.errors.count";

    const string AppServiceTag = "command-service";
    const string CommandTag    = "command-type";

    readonly Meter                                         _meter;
    readonly MetricsListener<CommandServiceMetricsContext> _listener;

    KeyValuePair<string, object?>[]? _customTags;

    public CommandServiceMetrics() {
        _meter = EventuousDiagnostics.GetMeter(MeterName);

        var duration   = _meter.CreateHistogram<double>(ProcessingRateName, "ms", "Command execution duration, milliseconds");
        var errorCount = _meter.CreateCounter<long>(ErrorCountName, "errors", "Number of failed commands");
        _listener = new(ListenerName, duration, errorCount, GetTags);

        return;

        TagList GetTags(CommandServiceMetricsContext ctx)
            => new(_customTags) {
                new(AppServiceTag, ctx.ServiceName),
                new(CommandTag, ctx.CommandName)
            };
    }

    public void Dispose() {
        _listener.Dispose();
        _meter.Dispose();
    }

    public void SetCustomTags(TagList customTags) => _customTags = customTags.ToArray();
}

record CommandServiceMetricsContext(string ServiceName, string CommandName);
