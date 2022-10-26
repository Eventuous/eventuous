using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics.Tracing;

namespace Eventuous.Diagnostics.Metrics;

public sealed class EventuousMetrics : IWithCustomTags, IDisposable {
    public static readonly string MeterName = EventuousDiagnostics.GetMeterName("core");

    readonly Meter                   _meter;
    readonly ActivityListener        _listener;
    KeyValuePair<string, object?>[]? _customTags;

    public EventuousMetrics() {
        _meter = EventuousDiagnostics.GetMeter(MeterName);

        var eventStoreMetric = _meter.CreateHistogram<double>(
            Constants.Components.EventStore,
            "ms",
            "Event store operation duration, milliseconds"
        );

        var appServiceMetric = _meter.CreateHistogram<double>(
            Constants.Components.AppService,
            "ms",
            "Application service operation duration, milliseconds"
        );

        _listener = new ActivityListener {
            ShouldListenTo  = x => x.Name == EventuousDiagnostics.InstrumentationName,
            ActivityStopped = Record
        };

        ActivitySource.AddActivityListener(_listener);

        void Record(Activity activity) {
            var dot = activity.OperationName.IndexOf('.');
            if (dot == -1) return;

            var prefix = activity.OperationName[..dot];

            switch (prefix) {
                case Constants.Components.AppService:
                    RecordWithTags(
                        appServiceMetric,
                        activity.Duration.TotalMilliseconds,
                        new KeyValuePair<string, object?>(
                            "command",
                            activity.GetTagItem(TelemetryTags.Eventuous.Command)
                        )
                    );

                    return;
                case Constants.Components.EventStore:
                    var operation          = activity.OperationName[(dot + 1)..];
                    var resourceSeparation = operation.IndexOf('/');

                    RecordWithTags(
                        eventStoreMetric,
                        activity.Duration.TotalMilliseconds,
                        new KeyValuePair<string, object?>(
                            "operation",
                            resourceSeparation > 0 ? operation[..resourceSeparation] : operation
                        )
                    );

                    break;
            }
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
        _listener.Dispose();
        _meter.Dispose();
    }

    public void SetCustomTags(TagList customTags) => _customTags = customTags.ToArray();
}
