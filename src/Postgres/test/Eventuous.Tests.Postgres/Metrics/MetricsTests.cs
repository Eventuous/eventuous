using Eventuous.Tests.OpenTelemetry;

namespace Eventuous.Tests.Postgres.Metrics;

[ClassDataSource<MetricsFixture>]
[NotInParallel]
public class MetricsTests(MetricsFixture fixture) : MetricsTestsBase(fixture) {
    [Test]
    [Retry(3)]
    public async Task ShouldMeasureSubscriptionGapCountBase_Postgres() {
        await ShouldMeasureSubscriptionGapCountBase();
    }

    [Test]
    [Retry(3)]
    public async Task ShouldMeasureSubscriptionDurationBase_Postgres() {
        await ShouldMeasureSubscriptionDurationBase();
    }
}

[ClassDataSource<MetricsFixture>]
[NotInParallel]
public class SubscriptionGapMetricsTests(MetricsFixture fixture) : SubscriptionGapMetricsTestsBase(fixture) {
    [Test]
    public async Task ShouldReportZeroGapWhenCaughtUp_Postgres(CancellationToken cancellationToken) {
        await ShouldReportZeroGapWhenCaughtUp(cancellationToken);
    }
}
