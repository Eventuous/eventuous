using Eventuous.Sql.Base.Producers;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Tests.OpenTelemetry;
using Testcontainers.SqlEdge;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.SqlServer.Metrics;

public class MetricsTests(ITestOutputHelper outputHelper)
    : MetricsTestsBase<MetricsFixture, SqlEdgeContainer, UniversalProducer, SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions>(outputHelper);
