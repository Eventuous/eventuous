using Eventuous.Sql.Base.Producers;
using Eventuous.SqlServer;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Tests.OpenTelemetry.Fixtures;
using Eventuous.Tests.SqlServer.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.SqlEdge;

namespace Eventuous.Tests.SqlServer.Metrics;

public class MetricsFixture
    : MetricsSubscriptionFixtureBase<SqlEdgeContainer, UniversalProducer, SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions> {
    readonly string _schemaName = GetSchemaName();

    protected override SqlEdgeContainer CreateContainer() => SqlContainer.Create();

    protected override void ConfigureSubscription(SqlServerStreamSubscriptionOptions options) {
        options.Schema           = _schemaName;
        options.Stream           = Stream;
        options.ConnectionString = Container.GetConnectionString();
    }

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousSqlServer(Container.GetConnectionString(), _schemaName, true);
        services.AddEventStore<SqlServerStore>();
        base.SetupServices(services);
    }
}
