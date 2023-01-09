// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics.Tracing;

namespace Eventuous.Diagnostics;

public sealed class PersistenceMetrics : IWithCustomTags, IDisposable {
    const string Category = "persistence";

    public static readonly string MeterName = EventuousDiagnostics.GetMeterName(Category);

    public const string ListenerName = $"{DiagnosticName.BaseName}.{Category}";

    readonly Meter             _meter;
    readonly Histogram<double> _duration;

    KeyValuePair<string, object?>[]? _customTags;

    public PersistenceMetrics() {
        _meter = EventuousDiagnostics.GetMeter(MeterName);

        _duration = _meter.CreateHistogram<double>(
            Constants.Components.EventStore,
            "ms",
            "Event store operation duration, milliseconds"
        );

        void Record(Activity activity) {
            var dot = activity.OperationName.IndexOf('.');
            if (dot == -1) return;

            var prefix = activity.OperationName[..dot];

            var operation = activity.OperationName[(dot + 1)..];
            var resourceSeparation = operation.IndexOf('/');

            RecordWithTags(
                eventStoreMetric,
                activity.Duration.TotalMilliseconds,
                new KeyValuePair<string, object?>(
                    "operation",
                    resourceSeparation > 0 ? operation[..resourceSeparation] : operation
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