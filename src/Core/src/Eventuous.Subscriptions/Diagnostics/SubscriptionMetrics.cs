// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Metrics;

// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable ConvertToLocalFunction
// ReSharper disable ParameterTypeCanBeEnumerable.Local

namespace Eventuous.Subscriptions.Diagnostics;

using Context;
using static SubscriptionsEventSource;

public sealed class SubscriptionMetrics : IWithCustomTags, IDisposable {
    const string MetricPrefix = DiagnosticName.BaseName;
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

    public SubscriptionMetrics(IEnumerable<GetSubscriptionEndOfStream> measures) {
        var getGaps = measures.ToArray();

        Dictionary<string, EndOfStream> streams = new();

        ObserveMetric<long> observeGapValues = () => ObserveGapValues(getGaps);

        _meter.CreateObservableGauge(
            GapCountMetricName,
            () => TryObserving(GapCountMetricName, observeGapValues),
            "events",
            "Gap between the last processed event and the stream tail"
        );

        _meter.CreateObservableGauge(
            GapTimeMetricName,
            () => TryObserving(GapTimeMetricName, () => ObserveTimeValues()),
            "s",
            "Subscription time lag, seconds"
        );

        _meter.CreateObservableGauge(
            CheckpointQueueLength,
            () => TryObserving(CheckpointQueueLength, () => _checkpointMetrics.Record()),
            "events",
            "Number of pending checkpoints"
        );

        var duration   = _meter.CreateHistogram<double>(ProcessingRateName, "ms", "Processing duration, milliseconds");
        var errorCount = _meter.CreateCounter<long>(ErrorCountName, "events", "Number of event processing failures");

        _listener = new(ListenerName, duration, errorCount, GetTags);

        return;

        IEnumerable<Measurement<double>> ObserveTimeValues()
            => streams.Values.Select(
                x => Measure(Math.Round((_checkpointMetrics.GetLastTimestamp(x.SubscriptionId) - x.Timestamp).TotalSeconds), x.SubscriptionId)
            );

        IEnumerable<Measurement<long>> ObserveGapValues(GetSubscriptionEndOfStream[] getEndOfStreams)
            => getEndOfStreams
                .Select(endOfStream => GetGap(endOfStream))
                .Where(x => x.Item1 != EndOfStream.Invalid)
                .Select(x => Measure((long)(x.Item1.Position - x.Item2), x.Item1.SubscriptionId));

        Measurement<T> Measure<T>(T value, string subscriptionId) where T : struct {
            if (_customTags.Length == 0) {
                return new(value, SubTag(subscriptionId));
            }

            var tags = new List<KeyValuePair<string, object?>>(_customTags) { SubTag(subscriptionId) };

            return new(value, tags);
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

        (EndOfStream, ulong) GetGap(GetSubscriptionEndOfStream getEndOfStream) {
            using var cts = new CancellationTokenSource(500);

            try {
                var t = getEndOfStream(cts.Token);

                var endOfStream = t.IsCompletedSuccessfully ? t.Result : t.NoContext().GetAwaiter().GetResult();
                streams[endOfStream.SubscriptionId] = endOfStream;
                var lastProcessed = _checkpointMetrics.GetLastCommitPosition(endOfStream.SubscriptionId);

                return (endOfStream, lastProcessed);
            } catch (Exception e) {
                Log.MetricCollectionFailed("Subscription Gap", e);

                return (EndOfStream.Invalid, 0);
            }
        }
    }

    static IEnumerable<Measurement<T>> TryObserving<T>(string metric, ObserveMetric<T> observe)
        where T : struct {
        try {
            return observe();
        } catch (Exception e) {
            Log.MetricCollectionFailed(metric, e);
            Activity.Current?.SetStatus(ActivityStatusCode.Error, e.Message);

            return Array.Empty<Measurement<T>>();
        }
    }

    static KeyValuePair<string, object?> SubTag(object? id) => new(SubscriptionIdTag, id);

    readonly Meter _meter = EventuousDiagnostics.GetMeter(MeterName);

    readonly MetricsListener<SubscriptionMetricsContext> _listener;

    readonly CheckpointCommitMetrics _checkpointMetrics = new();

    KeyValuePair<string, object?>[] _customTags = EventuousDiagnostics.Tags;

    public void SetCustomTags(TagList customTags) => _customTags = _customTags.Concat(customTags).ToArray();

    public void Dispose() {
        _listener.Dispose();
        _checkpointMetrics.Dispose();
        _meter.Dispose();
    }

    internal record SubscriptionMetricsContext(string EventHandler, IMessageConsumeContext Context);

    delegate IEnumerable<Measurement<T>> ObserveMetric<T>() where T : struct;
}
