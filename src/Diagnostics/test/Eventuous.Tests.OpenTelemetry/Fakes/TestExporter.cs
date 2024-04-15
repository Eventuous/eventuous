namespace Eventuous.Tests.OpenTelemetry.Fakes;

[ExportModes(ExportModes.Pull)]
public class TestExporter : BaseExporter<Metric>, IPullMetricExporter {
    public override ExportResult Export(in Batch<Metric> batch) {
        Batch = batch;

        return ExportResult.Success;
    }

    Batch<Metric> Batch { get; set; }

    public Func<int, bool> Collect { get; set; } = null!;

    public MetricValue[] CollectValues() {
        var values = new List<MetricValue>();

        foreach (var metric in Batch) {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (metric == null) continue;

            foreach (ref readonly var metricPoint in metric.GetMetricPoints()) {
                var tags = new List<(string, object?)>();

                foreach (var (key, value) in metricPoint.Tags) {
                    tags.Add((key, value));
                }

                var metricValue = metric.MetricType switch {
                    MetricType.Histogram   => metricPoint.GetHistogramSum() / metricPoint.GetHistogramCount(),
                    MetricType.DoubleGauge => metricPoint.GetGaugeLastValueDouble(),
                    MetricType.LongGauge   => metricPoint.GetGaugeLastValueLong(),
                    _                      => throw new ArgumentOutOfRangeException()
                };

                values.Add(
                    new MetricValue(
                        metric.Name,
                        tags.Select(x => x.Item1).ToArray(),
                        tags.Select(x => x.Item2).ToArray()!,
                        metricValue
                    )
                );
            }
        }

        return values.ToArray();
    }
}
