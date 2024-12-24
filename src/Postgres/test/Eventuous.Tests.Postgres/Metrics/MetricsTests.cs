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
}

