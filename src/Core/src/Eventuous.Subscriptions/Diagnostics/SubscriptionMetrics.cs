using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;

// ReSharper disable ParameterTypeCanBeEnumerable.Local

namespace Eventuous.Subscriptions.Diagnostics;

public sealed class SubscriptionMetrics : IDisposable {
    const string MetricPrefix = "eventuous";
    const string Category     = "subscription";

    public static readonly string MeterName = EventuousDiagnostics.GetMeterName(Category);

    public const string GapCountMetricName = $"{MetricPrefix}.{Category}.gap.count";
    public const string GapTimeMetricName  = $"{MetricPrefix}.{Category}.gap.seconds";
    public const string ProcessingRateName = $"{MetricPrefix}.{Category}.duration";

    public SubscriptionMetrics(IEnumerable<GetSubscriptionGap> measures) {
        _meter = EventuousDiagnostics.GetMeter(MeterName);
        var getGaps = measures.ToArray();

        _meter.CreateObservableGauge(
            GapCountMetricName,
            () => ObserveGapValues(getGaps),
            "events",
            "Gap between the last processed event and the stream end"
        );

        IEnumerable<SubscriptionGap>? gaps = null;

        _meter.CreateObservableGauge(
            GapTimeMetricName,
            ObserveTimeValues,
            "s",
            "Subscription time lag, seconds"
        );

        var histogram = _meter.CreateHistogram<double>(ProcessingRateName, "s", "Processing duration, seconds");

        _listener = new ActivityListener {
            ShouldListenTo = x => x.Name == EventuousDiagnostics.InstrumentationName,
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                var subId = activity.GetTagItem(TelemetryTags.Eventuous.Subscription);
                if (subId == null) return;

                histogram.Record(activity.Duration.TotalSeconds, SubTag(subId));
            }
        };

        KeyValuePair<string, object?> SubTag(object? id) => new("subscription-id", id);

        IEnumerable<Measurement<double>> ObserveTimeValues()
            => gaps?.Select(x => new Measurement<double>(x.TimeGap.TotalSeconds, SubTag(x.SubscriptionId)))
            ?? Array.Empty<Measurement<double>>();

        IEnumerable<Measurement<long>> ObserveGapValues(GetSubscriptionGap[] gapMeasure) {
            gaps = gapMeasure.Select(GetGap);

            return gaps.Select(x => new Measurement<long>((long)x.PositionGap, SubTag(x.SubscriptionId)));
        }

        SubscriptionGap GetGap(GetSubscriptionGap gapMeasure) {
            var cts = new CancellationTokenSource(5000);
            var t   = gapMeasure(cts.Token);

            return t.IsCompleted ? t.Result : t.GetAwaiter().GetResult();
        }
    }

    readonly Meter            _meter;
    readonly ActivityListener _listener;

    public void Dispose() {
        _listener.Dispose();
        _meter.Dispose();
    }
}