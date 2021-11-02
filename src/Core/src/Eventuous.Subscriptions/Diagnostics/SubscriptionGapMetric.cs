using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;

namespace Eventuous.Subscriptions.Diagnostics;

public sealed class SubscriptionGapMetric : IDisposable {
    const string Category = "subscription";

    public static string MeterName = EventuousDiagnostics.GetMeterName(Category);
    
    public SubscriptionGapMetric(IEnumerable<ISubscriptionGapMeasure> measures) {
        Meter = EventuousDiagnostics.GetMeter("subscription");

        foreach (var measure in measures) {
            var gap = GetGap(measure);

            Meter.CreateObservableGauge(
                $"subscription-{gap.SubscriptionId}",
                () => ObserveValues(measure),
                "events",
                "Number of unprocessed events"
            );
        }

        IEnumerable<Measurement<long>> ObserveValues(ISubscriptionGapMeasure gapMeasure)
            => new[] { new Measurement<long>((long)GetGap(gapMeasure).PositionGap) };

        SubscriptionGap GetGap(ISubscriptionGapMeasure gapMeasure) {
            var cts = new CancellationTokenSource(5000);
            return gapMeasure.GetSubscriptionGap(cts.Token).GetAwaiter().GetResult();
        }
    }

    Meter Meter { get; }

    public void Dispose() => Meter.Dispose();
}