using Eventuous.Tests.OpenTelemetry;

namespace Eventuous.Tests.KurrentDB.Metrics;

[ClassDataSource<MetricsFixture>]
[NotInParallel]
public class MetricsTests(MetricsFixture fixture) : MetricsTestsBase(fixture) {
    [Test]
    [Retry(3)]
    public async Task ShouldMeasureSubscriptionGapCountBase_Esdb() {
        await ShouldMeasureSubscriptionGapCountBase();
    }
}
