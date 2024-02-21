using Eventuous.Postgresql.Subscriptions;
using Eventuous.Sql.Base.Producers;
using Eventuous.Tests.OpenTelemetry;
using Testcontainers.PostgreSql;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.Postgres.Metrics;

[Collection("Database")]
public class MetricsTests(ITestOutputHelper outputHelper)
    : MetricsTestsBase<MetricsFixture, PostgreSqlContainer, UniversalProducer, PostgresStreamSubscription, PostgresStreamSubscriptionOptions>(outputHelper);
