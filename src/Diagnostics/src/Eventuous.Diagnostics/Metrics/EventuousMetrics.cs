using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics.Tracing;

namespace Eventuous.Diagnostics.Metrics;

public sealed class EventuousMetrics : IDisposable {
    public static readonly string MeterName = EventuousDiagnostics.GetMeterName("core");

    readonly Meter            _meter;
    readonly ActivityListener _listener;

    public EventuousMetrics() {
        _meter = EventuousDiagnostics.GetMeter(MeterName);

        var eventStoreMetric = _meter.CreateHistogram<double>(
            Constants.EventStorePrefix,
            "s",
            "Event store operation duration, seconds"
        );

        var appServiceMetric = _meter.CreateHistogram<double>(
            "appservice",
            "s",
            "Application service operation duration, seconds"
        );

        _listener = new ActivityListener {
            ShouldListenTo  = x => x.Name == EventuousDiagnostics.InstrumentationName,
            Sample          = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = Record
        };
        ActivitySource.AddActivityListener(_listener);

        void Record(Activity activity) {
            if (activity.OperationName == Constants.HandleCommand) {
                appServiceMetric.Record(
                    activity.Duration.TotalSeconds,
                    new KeyValuePair<string, object?>("command", activity.GetTagItem(Constants.CommandTag))
                );

                return;
            }

            if (activity.OperationName.StartsWith(Constants.EventStorePrefix)) {
                eventStoreMetric.Record(
                    activity.Duration.TotalSeconds,
                    new KeyValuePair<string, object?>("operation", activity.OperationName)
                );
            }
        }
    }

    public void Dispose() {
        _listener.Dispose();
        _meter.Dispose();
    }
}