// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics.Metrics;

namespace Eventuous.Diagnostics;

using static Tracing.Constants.Components;

public sealed class CommandServiceMetrics : IWithCustomTags, IDisposable {
    public readonly static string MeterName = EventuousDiagnostics.GetMeterName(Category);

    public const string ListenerName = $"{DiagnosticName.BaseName}.{Category}";

    const string Category      = "application";
    const string AppServiceTag = "command-service";
    const string CommandTag    = "command-type";

    readonly Meter                                         _meter;
    readonly MetricsListener<CommandServiceMetricsContext> _listener;

    KeyValuePair<string, object?>[]? _customTags;

    public CommandServiceMetrics() {
        _meter = EventuousDiagnostics.GetMeter(MeterName);

        var duration   = _meter.CreateHistogram<double>(CommandService, "ms", "Command execution duration, milliseconds");
        var errorCount = _meter.CreateCounter<long>($"{CommandService}.errors", "errors", "Number of failed commands");
        _listener = new MetricsListener<CommandServiceMetricsContext>(ListenerName, duration, errorCount, GetTags);

        TagList GetTags(CommandServiceMetricsContext ctx)
            => new(_customTags) {
                new KeyValuePair<string, object?>(AppServiceTag, ctx.ServiceName),
                new KeyValuePair<string, object?>(CommandTag, ctx.CommandName)
            };
    }

    public void Dispose() {
        _listener.Dispose();
        _meter.Dispose();
    }

    public void SetCustomTags(TagList customTags) => _customTags = customTags.ToArray();
}

record CommandServiceMetricsContext(string ServiceName, string CommandName);
