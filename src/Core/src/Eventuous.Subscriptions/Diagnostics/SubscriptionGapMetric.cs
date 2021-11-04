using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;

namespace Eventuous.Subscriptions.Diagnostics;

public sealed class SubscriptionGapMetric : IDisposable {
    const string Category = "subscription";

    public static readonly string MeterName = EventuousDiagnostics.GetMeterName(Category);

    public const string MetricName = "subscription-gap-count";

    public SubscriptionGapMetric(IEnumerable<GetSubscriptionGap> measures) {
        Meter = EventuousDiagnostics.GetMeter(MeterName);

        foreach (var measure in measures) {
            var gap = GetGap(measure);

            var tags = new[] {
                new KeyValuePair<string, object?>("subscription-id", gap.SubscriptionId)
            };

            Meter.CreateObservableGauge(
                MetricName,
                () => ObserveValues(measure, tags),
                "events",
                "Number of unprocessed events"
            );
        }

        IEnumerable<Measurement<long>> ObserveValues(
            GetSubscriptionGap              gapMeasure,
            KeyValuePair<string, object?>[] tags
        ) {
            var gap = GetGap(gapMeasure);
            return new[] { new Measurement<long>((long)gap.PositionGap, tags) };
        }

        SubscriptionGap GetGap(GetSubscriptionGap gapMeasure) {
            var cts = new CancellationTokenSource(5000);
            var t   = gapMeasure(cts.Token);
            return t.IsCompleted ? t.Result : t.GetAwaiter().GetResult();
        }
    }

    Meter Meter { get; }

    public void Dispose() => Meter.Dispose();
}