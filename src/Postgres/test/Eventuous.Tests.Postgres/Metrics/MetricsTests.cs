using Eventuous.Postgresql.Subscriptions;
using Eventuous.Sql.Base.Producers;
using Eventuous.Tests.OpenTelemetry;
using Testcontainers.PostgreSql;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.Postgres.Metrics;

public class MetricsTests : MetricsTestsBase<MetricsFixture, PostgreSqlContainer, UniversalProducer, PostgresStreamSubscription, PostgresStreamSubscriptionOptions> {
    [Test]
    public async Task ShouldMeasureSubscriptionGapCount() {
        await ShouldMeasureSubscriptionGapCountBase();
    }

    [Before(Test)]
    public async Task Setup() => await InitializeAsync();
    
    [After(Test)]
    public async Task TearDown() => await DisposeAsync();
}

