using Eventuous.Sql.Base.Producers;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Tests.OpenTelemetry;
using Testcontainers.SqlEdge;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.SqlServer.Metrics;

public class MetricsTests : MetricsTestsBase<MetricsFixture, SqlEdgeContainer, UniversalProducer, SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions> {
    [Test]
    public async Task ShouldMeasureSubscriptionGapCount() {
        await ShouldMeasureSubscriptionGapCountBase();
    }

    [Before(Test)]
    public async Task Setup() => await InitializeAsync();

    [After(Test)]
    public async Task TearDown() => await DisposeAsync();
}
