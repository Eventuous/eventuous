using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;
// ReSharper disable ParameterTypeCanBeEnumerable.Local

namespace Eventuous.Subscriptions.Diagnostics;

public sealed class SubscriptionGapMetric : IDisposable {
    const string Category = "subscription";

    public static readonly string MeterName = EventuousDiagnostics.GetMeterName(Category);

    public const string MetricName = "subscription-gap-count";

    public SubscriptionGapMetric(IEnumerable<GetSubscriptionGap> measures) {
        Meter = EventuousDiagnostics.GetMeter(MeterName);
        var getGaps = measures.ToArray();

        Meter.CreateObservableGauge(
            MetricName,
            () => ObserveValues(getGaps),
            "events",
            "Number of unprocessed events"
        );

        IEnumerable<Measurement<long>> ObserveValues(GetSubscriptionGap[] gapMeasure)
            => gapMeasure.Select(GetGap);

        Measurement<long> GetGap(GetSubscriptionGap gapMeasure) {
            var cts = new CancellationTokenSource(5000);
            var t   = gapMeasure(cts.Token);

            var (subscriptionId, positionGap, _) =
                t.IsCompleted ? t.Result : t.GetAwaiter().GetResult();

            return new Measurement<long>(
                (long)positionGap,
                new KeyValuePair<string, object?>("subscription-id", subscriptionId)
            );
        }
    }

    Meter Meter { get; }

    public void Dispose() => Meter.Dispose();
}