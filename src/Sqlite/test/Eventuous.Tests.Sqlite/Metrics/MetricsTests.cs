using Eventuous.Tests.OpenTelemetry;

namespace Eventuous.Tests.Sqlite.Metrics;

[ClassDataSource<MetricsFixture>]
[NotInParallel]
public class MetricsTests(MetricsFixture fixture) : MetricsTestsBase(fixture) {
    [Test]
    [Retry(3)]
    public async Task ShouldMeasureSubscriptionGapCountBase_Sqlite() {
        await ShouldMeasureSubscriptionGapCountBase();
    }
}

[ClassDataSource<MetricsFixture>]
[NotInParallel]
public class SubscriptionGapMetricsTests(MetricsFixture fixture) : SubscriptionGapMetricsTestsBase(fixture) {
    [Test]
    public async Task ShouldReportZeroGapWhenCaughtUp_Sqlite(CancellationToken cancellationToken) {
        await ShouldReportZeroGapWhenCaughtUp(cancellationToken);
    }
}
