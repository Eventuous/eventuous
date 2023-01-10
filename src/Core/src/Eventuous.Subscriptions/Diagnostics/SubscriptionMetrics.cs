// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Metrics;
using Eventuous.Subscriptions.Context;
using Eventuous.Tools;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;
// ReSharper disable ConvertClosureToMethodGroup

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
    public const string EventHandlerTag   = "event-handler";

    public const string ListenerName = $"{DiagnosticName.BaseName}.{Category}";

    public SubscriptionMetrics(IEnumerable<GetSubscriptionGap> measures) {
        var getGaps = measures.ToArray();
        Dictionary<string, SubscriptionGap> gaps = new();

        _meter.CreateObservableGauge(
            GapCountMetricName,
            () => TryObserving(GapCountMetricName, () => ObserveGapValues(getGaps)),
            "events",
            "Gap between the last processed event and the stream tail"
        );

        _meter.CreateObservableGauge(
            GapTimeMetricName,
            () => TryObserving(GapTimeMetricName, ObserveTimeValues),
            "s",
            "Subscription time lag, seconds"
        );

        _meter.CreateObservableGauge(
            CheckpointQueueLength,
            () => TryObserving(CheckpointQueueLength, _checkpointMetrics.Value.Record),
            "events",
            "Number of pending checkpoints"
        );

        var duration = _meter.CreateHistogram<double>(ProcessingRateName, "ms", "Processing duration, milliseconds");
        var errorCount = _meter.CreateCounter<long>(ErrorCountName, "events", "Number of event processing failures");

        _listener = new MetricsListener<SubscriptionMetricsContext>(ListenerName, duration, errorCount, GetTags);

        IEnumerable<Measurement<double>> ObserveTimeValues()
            => gaps.Values.Select(x => Measure(x.TimeGap.TotalSeconds, x.SubscriptionId));

        IEnumerable<Measurement<long>> ObserveGapValues(GetSubscriptionGap[] gapMeasure)
            => gapMeasure
                .Select(gap => GetGap(gap))
                .Where(x => x != SubscriptionGap.Invalid)
                .Select(x => Measure((long)x.PositionGap, x.SubscriptionId));

        Measurement<T> Measure<T>(T value, string subscriptionId) where T : struct {
            if (_customTags.Length == 0) {
                return new Measurement<T>(value, SubTag(subscriptionId));
            }

            var tags = new List<KeyValuePair<string, object?>>(_customTags) { SubTag(subscriptionId) };
            return new Measurement<T>(value, tags);
        }

        TagList GetTags(SubscriptionMetricsContext ctx) {
            var subTag = SubTag(ctx.Context.SubscriptionId);

            var tags = new TagList(_customTags) {
                subTag,
                new(MessageTypeTag, ctx.Context.MessageType),
                new(EventHandlerTag, ctx.EventHandler)
            };

            if (ctx.Context is AsyncConsumeContext asyncConsumeContext) {
                tags.Add(PartitionIdTag, asyncConsumeContext.PartitionId);
            }

            return tags;
        }

        SubscriptionGap GetGap(GetSubscriptionGap gapMeasure) {
            var cts = new CancellationTokenSource(500);

            try {
                var t = gapMeasure(cts.Token);

                var gap = t.IsCompleted ? t.Result : t.NoContext().GetAwaiter().GetResult();
                gaps[gap.SubscriptionId] = gap;
                return gap;
            }
            catch (Exception e) {
                Log.MetricCollectionFailed("Subscription Gap", e);
                return SubscriptionGap.Invalid;
            }
        }
    }

    static IEnumerable<Measurement<T>> TryObserving<T>(string metric, Func<IEnumerable<Measurement<T>>> observe)
        where T : struct {
        try {
            return observe();
        }
        catch (Exception e) {
            Log.MetricCollectionFailed(metric, e);
            Activity.Current?.SetStatus(ActivityStatusCode.Error, e.Message);
            return Array.Empty<Measurement<T>>();
        }
    }

    static KeyValuePair<string, object?> SubTag(object? id) => new(SubscriptionIdTag, id);

    readonly Meter _meter = EventuousDiagnostics.GetMeter(MeterName);

    readonly MetricsListener<SubscriptionMetricsContext> _listener;

    readonly Lazy<CheckpointCommitMetrics> _checkpointMetrics = new(() => new CheckpointCommitMetrics());

    KeyValuePair<string, object?>[] _customTags = EventuousDiagnostics.Tags;

    public void SetCustomTags(TagList customTags) => _customTags = _customTags.Concat(customTags).ToArray();

    public void Dispose() {
        _meter.Dispose();
        _listener.Dispose();
        if (_checkpointMetrics.IsValueCreated) _checkpointMetrics.Value.Dispose();
    }

    internal record SubscriptionMetricsContext(string EventHandler, IMessageConsumeContext Context);
}