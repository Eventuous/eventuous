using Eventuous.Tests.OpenTelemetry;

namespace Eventuous.Tests.SqlServer.Metrics;

[ClassDataSource<MetricsFixture>]
[NotInParallel]
public class MetricsTests(MetricsFixture fixture) : MetricsTestsBase(fixture) {
    [Test]
    [Retry(3)]
    public async Task ShouldMeasureSubscriptionGapCountBase_SqlServer() {
        await ShouldMeasureSubscriptionGapCountBase();
    }
}

[ClassDataSource<MetricsFixture>]
[NotInParallel]
public class SubscriptionGapMetricsTests(MetricsFixture fixture) : SubscriptionGapMetricsTestsBase(fixture) {
    [Test]
    public async Task ShouldReportZeroGapWhenCaughtUp_SqlServer(CancellationToken cancellationToken) {
        await ShouldReportZeroGapWhenCaughtUp(cancellationToken);
    }
}
