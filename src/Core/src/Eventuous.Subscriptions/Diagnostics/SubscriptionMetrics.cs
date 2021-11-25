using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;

// ReSharper disable ParameterTypeCanBeEnumerable.Local

namespace Eventuous.Subscriptions.Diagnostics;

public sealed class SubscriptionMetrics : IDisposable {
    const string MetricPrefix = "eventuous";
    const string Category     = "subscription";

    public static readonly string MeterName = EventuousDiagnostics.GetMeterName(Category);

    public const string GapCountMetricName    = $"{MetricPrefix}.{Category}.gap.count";
    public const string GapTimeMetricName     = $"{MetricPrefix}.{Category}.gap.seconds";
    public const string ProcessingRateName    = $"{MetricPrefix}.{Category}.duration";
    public const string ErrorCountName        = $"{MetricPrefix}.{Category}.errors.count";
    public const string CheckpointQueueLength = $"{MetricPrefix}.{Category}.pendingCheckpoint.count";

    internal const string SubscriptionIdTag = "subscription-id";

    const string MessageTypeTag = "message-type";
    const string PartitionIdTag = "partition";

    public SubscriptionMetrics(IEnumerable<GetSubscriptionGap> measures) {
        _meter = EventuousDiagnostics.GetMeter(MeterName);
        var getGaps = measures.ToArray();
        _checkpointMetrics = new CheckpointCommitMetrics();
        IEnumerable<SubscriptionGap>? gaps = null;

        _meter.CreateObservableGauge(
            GapCountMetricName,
            () => ObserveGapValues(getGaps),
            "events",
            "Gap between the last processed event and the stream tail"
        );

        _meter.CreateObservableGauge(
            GapTimeMetricName,
            ObserveTimeValues,
            "s",
            "Subscription time lag, seconds"
        );

        _meter.CreateObservableGauge(
            CheckpointQueueLength,
            _checkpointMetrics.Record,
            "events",
            "Number of pending checkpoints"
        );

        var histogram  = _meter.CreateHistogram<double>(ProcessingRateName, "s", "Processing duration, seconds");
        var errorCount = _meter.CreateCounter<long>(ErrorCountName, "events", "Number of event processing failures");

        _listener = new ActivityListener {
            ShouldListenTo = x => x.Name == EventuousDiagnostics.InstrumentationName,
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                var subId = activity.GetTagItem(TelemetryTags.Eventuous.Subscription);
                if (subId == null) return;

                var subTag       = SubTag(subId);
                var typeTag      = GetTag(MessageTypeTag, activity.GetTagItem(TelemetryTags.Message.Type));
                var partitionTag = GetTag(PartitionIdTag, activity.GetTagItem(TelemetryTags.Eventuous.Partition));
                histogram.Record(activity.Duration.TotalSeconds, subTag, typeTag, partitionTag);

                if (activity.Status == ActivityStatusCode.Error) {
                    errorCount.Add(1, subTag, typeTag);
                }
            }
        };

        KeyValuePair<string, object?> GetTag(string  key, object? id) => new(key, id);
        KeyValuePair<string, object?> SubTag(object? id) => new(SubscriptionIdTag, id);

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

    readonly Meter                   _meter;
    readonly ActivityListener        _listener;
    readonly CheckpointCommitMetrics _checkpointMetrics;

    public void Dispose() {
        _listener.Dispose();
        _meter.Dispose();
        _checkpointMetrics.Dispose();
    }
}