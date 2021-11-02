using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Checkpoints;

namespace Eventuous.Subscriptions.Diagnostics;

public class SubscriptionGapMetric : IDisposable {
    public SubscriptionGapMetric() {
        Meter = EventuousDiagnostics.GetMeter("subscription");
        ActivitySource.AddActivityListener(new ActivityListener {
            Sample = _ => ActivitySamplingResult.AllData,
            ShouldListenTo = x => x.Name == EventuousDiagnostics.InstrumentationName,
            ActivityStopped = Stopped
        });
        // diagnostics.Meter.CreateObservableGauge(
        //     $"subscription-{subscriptionId}",
        //     ObserveValues,
        //     "events",
        //     "Number of unprocessed events"
        // );
        //
        // IEnumerable<Measurement<long>> ObserveValues() {
        //     var cts = new CancellationTokenSource(5000);
        //     var gap = gapMeasure.GetSubscriptionGap(cts.Token).GetAwaiter().GetResult();
        //     return new[] { new Measurement<long>((long)gap.PositionGap) };
        // }
    }

    void Stopped(Activity activity) {
        switch (activity.OperationName) {
            case MeasuredCheckpointStore.ReadOperationName:
                break;
            case MeasuredCheckpointStore.WriteOperationName:
                break;
        }
    }

    Dictionary<string, ObservableGauge<long>> _gauges = new();

    ObservableGauge<long> GetGauge(string id) {
        // var gauge = Meter.C
        if (!_gauges.ContainsKey(id))
            _gauges.TryAdd(id, Meter.CreateObservableGauge(id))
    }

    Meter Meter { get; }

    public void Dispose() => Meter.Dispose();
}