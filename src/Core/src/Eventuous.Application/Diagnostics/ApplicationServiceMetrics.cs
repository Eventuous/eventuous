// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics.Metrics;
using static Eventuous.Diagnostics.Tracing.Constants.Components;

namespace Eventuous.Diagnostics;

public sealed class ApplicationServiceMetrics : IWithCustomTags, IDisposable {
    const string Category = "application";

    public static readonly string MeterName = EventuousDiagnostics.GetMeterName(Category);

    public const string ListenerName = $"{DiagnosticName.BaseName}.{Category}";

    public const string AppServiceTag = "command-service";
    public const string CommandTag    = "command-type";

    readonly Meter                                     _meter;
    readonly MetricsListener<AppServiceMetricsContext> _listener;

    KeyValuePair<string, object?>[]? _customTags;

    public ApplicationServiceMetrics() {
        _meter = EventuousDiagnostics.GetMeter(MeterName);

        var duration =
            _meter.CreateHistogram<double>(AppService, "ms", "Command execution duration, milliseconds");

        var errorCount = _meter.CreateCounter<long>($"{AppService}.errors", "errors", "Number of failed commands");
        _listener = new MetricsListener<AppServiceMetricsContext>(ListenerName, duration, errorCount, GetTags);

        TagList GetTags(AppServiceMetricsContext ctx)
            => new TagList(_customTags) {
                new(AppServiceTag, ctx.ServiceName),
                new(CommandTag, ctx.CommandName),
            };
    }

    public void Dispose() {
        _listener.Dispose();
        _meter.Dispose();
    }

    public void SetCustomTags(TagList customTags) => _customTags = customTags.ToArray();
}

record AppServiceMetricsContext(string ServiceName, string CommandName);