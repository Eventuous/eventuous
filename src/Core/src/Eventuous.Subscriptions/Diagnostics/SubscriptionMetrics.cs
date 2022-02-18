using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

// ReSharper disable ParameterTypeCanBeEnumerable.Local

namespace Eventuous.Subscriptions.Diagnostics;

public sealed class SubscriptionMetrics : IWithCustomTags, IDisposable {
    const string MetricPrefix = "eventuous";
    const string Category     = "subscription";

    public static readonly string MeterName = EventuousDiagnostics.GetMeterName(Category);

    public const string GapCountMetricName    = $"{MetricPrefix}.{Category}.gap.count";
    public const string GapTimeMetricName     = $"{MetricPrefix}.{Category}.gap.seconds";
    public const string ProcessingRateName    = $"{MetricPrefix}.{Category}.duration";
    public const string ErrorCountName        = $"{MetricPrefix}.{Category}.errors.count";
    public const string CheckpointQueueLength = $"{MetricPrefix}.{Category}.pendingCheckpoint.count";

    public const string SubscriptionIdTag = "subscription-id";
    public const string MessageTypeTag    = "message-type";
    public const string PartitionIdTag    = "partition";

    public SubscriptionMetrics(IEnumerable<GetSubscriptionGap> measures) {
        _meter = EventuousDiagnostics.GetMeter(MeterName);
        var getGaps = measures.ToArray();
        _checkpointMetrics = new Lazy<CheckpointCommitMetrics>(() => new CheckpointCommitMetrics());
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
            _checkpointMetrics.Value.Record,
            "events",
            "Number of pending checkpoints"
        );

        var histogram  = _meter.CreateHistogram<double>(ProcessingRateName, "ms", "Processing duration, milliseconds");
        var errorCount = _meter.CreateCounter<long>(ErrorCountName, "events", "Number of event processing failures");

        _listener = new ActivityListener {
            ShouldListenTo  = x => x.Name == EventuousDiagnostics.InstrumentationName,
            Sample          = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = x => ActivityStopped(histogram, errorCount, x)
        };

        ActivitySource.AddActivityListener(_listener);

        IEnumerable<Measurement<double>> ObserveTimeValues()
            => gaps?
                   .Select(x => Measure(x.TimeGap.TotalSeconds, x.SubscriptionId))
            ?? Array.Empty<Measurement<double>>();

        IEnumerable<Measurement<long>> ObserveGapValues(GetSubscriptionGap[] gapMeasure) {
            gaps = gapMeasure.Select(GetGap).Where(x => x != SubscriptionGap.Invalid);

            return gaps.Select(x => Measure((long)x.PositionGap, x.SubscriptionId));
        }

        Measurement<T> Measure<T>(T value, string subscriptionId) where T : struct {
            if (_customTags == null) {
                return new Measurement<T>(value, SubTag(subscriptionId));
            }

            var tags = new List<KeyValuePair<string, object?>>(_customTags) { SubTag(subscriptionId) };
            return new Measurement<T>(value, tags);
        }
    }

    static KeyValuePair<string, object?> GetTag(string key, object? id) => new(key, id);

    static KeyValuePair<string, object?> SubTag(object? id) => new(SubscriptionIdTag, id);

    void ActivityStopped(Histogram<double> histogram, Counter<long> errorCount, Activity activity) {
        if (activity.OperationName != TracingConstants.ConsumerOperation) return;

        var subId = activity.GetTagItem(TelemetryTags.Eventuous.Subscription);
        if (subId == null) return;

        var subTag       = SubTag(subId);
        var typeTag      = GetTag(MessageTypeTag, activity.GetTagItem(TelemetryTags.Message.Type));
        var partitionTag = GetTag(PartitionIdTag, activity.GetTagItem(TelemetryTags.Eventuous.Partition));

        var tags = _customTags == null ? new TagList() : new TagList(_customTags);
        tags.Add(subTag);
        tags.Add(typeTag);
        tags.Add(partitionTag);

        histogram.Record(activity.Duration.TotalMilliseconds, tags);

        if (activity.Status == ActivityStatusCode.Error) {
            errorCount.Add(1, tags);
        }
    }

    static SubscriptionGap GetGap(GetSubscriptionGap gapMeasure) {
        var cts = new CancellationTokenSource(5000);

        try {
            var t = gapMeasure(cts.Token);

            return t.IsCompleted ? t.Result : t.NoContext().GetAwaiter().GetResult();
        }
        catch (Exception e) {
            Log.MetricCollectionFailed("Subscription Gap", e);
            return SubscriptionGap.Invalid;
        }
    }

    readonly Meter                         _meter;
    readonly ActivityListener              _listener;
    readonly Lazy<CheckpointCommitMetrics> _checkpointMetrics;
    KeyValuePair<string, object?>[]?       _customTags;

    public void Dispose() {
        _listener.Dispose();
        _meter.Dispose();
        if (_checkpointMetrics.IsValueCreated) _checkpointMetrics.Value.Dispose();
    }

    public void SetCustomTags(TagList customTags) => _customTags = customTags.ToArray();
}
