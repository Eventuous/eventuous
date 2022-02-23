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
        _meter      = EventuousDiagnostics.GetMeter(MeterName);

        var eventStoreMetric = _meter.CreateHistogram<double>(
            Constants.EventStorePrefix,
            "ms",
            "Event store operation duration, milliseconds"
        );

        var appServiceMetric = _meter.CreateHistogram<double>(
            Constants.AppServicePrefix,
            "ms",
            "Application service operation duration, milliseconds"
        );

        _listener = new ActivityListener {
            ShouldListenTo  = x => x.Name == EventuousDiagnostics.InstrumentationName,
            Sample          = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = Record
        };

        ActivitySource.AddActivityListener(_listener);

        void Record(Activity activity) {
            var dot    = activity.OperationName.IndexOf('.');
            var prefix = activity.OperationName[..dot];
            switch (prefix) {
                case Constants.AppServicePrefix:
                    RecordWithTags(
                        appServiceMetric,
                        activity.Duration.TotalMilliseconds,
                        new KeyValuePair<string, object?>("command", activity.GetTagItem(Constants.CommandTag))
                    );
                    return;
                case Constants.EventStorePrefix:
                    RecordWithTags(
                        eventStoreMetric,
                        activity.Duration.TotalMilliseconds,
                        new KeyValuePair<string, object?>("operation", activity.OperationName)
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
