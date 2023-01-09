// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics.Metrics;
using Eventuous.Diagnostics.Tracing;
using static Eventuous.Diagnostics.Tracing.Constants.Components;

namespace Eventuous.Diagnostics;

public sealed class ApplicationServiceMetrics : IWithCustomTags, IDisposable {
    const string Category = "application";

    public static readonly string MeterName = EventuousDiagnostics.GetMeterName(Category);

    public const string ListenerName = $"{DiagnosticName.BaseName}.{Category}";

    readonly Meter _meter;

    KeyValuePair<string, object?>[]? _customTags;

    public ApplicationServiceMetrics() {
        _meter = EventuousDiagnostics.GetMeter(MeterName);

        var duration =
            _meter.CreateHistogram<double>(AppService, "ms", "Application service operation duration, milliseconds");

        var errorCount = _meter.CreateCounter<long>($"{AppService}.errors", "errors", "Number of failed commands");
        _listener = new MetricsListener<IMessageConsumeContext>(ListenerName, duration, errorCount, GetTags);

        void Record(Activity activity) {
            var dot = activity.OperationName.IndexOf('.');
            if (dot == -1) return;

            var prefix = activity.OperationName[..dot];

            RecordWithTags(
                duration,
                activity.Duration.TotalMilliseconds,
                new KeyValuePair<string, object?>(
                    "command",
                    activity.GetTagItem(TelemetryTags.Eventuous.Command)
                )
            );
        }

        void RecordWithTags(Histogram<double> histogram, double value, KeyValuePair<string, object?> tag) {
            if (_customTags == null) {
                histogram.Record(value, tag);
                return;
            }

            var tags = new TagList(_customTags) { tag };
            histogram.Record(value, tags);
        }
    }

    public void Dispose() {
        _meter.Dispose();
    }

    public void SetCustomTags(TagList customTags) => _customTags = customTags.ToArray();
}