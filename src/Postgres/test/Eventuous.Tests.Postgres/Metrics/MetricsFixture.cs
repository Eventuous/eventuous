using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Sql.Base.Producers;
using Eventuous.Tests.OpenTelemetry.Fixtures;
using Eventuous.Tests.Postgres.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Metrics;

public class MetricsFixture : MetricsSubscriptionFixtureBase<PostgreSqlContainer, UniversalProducer, PostgresStreamSubscription, PostgresStreamSubscriptionOptions> {
    readonly string _schemaName = GetSchemaName();

    protected override PostgreSqlContainer CreateContainer() => PostgresContainer.Create();

    protected override void ConfigureSubscription(PostgresStreamSubscriptionOptions options) {
        options.Schema = _schemaName;
        options.Stream = Stream;
    }

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousPostgres(Container.GetConnectionString(), _schemaName, true);
        services.AddEventStore<PostgresStore>();
        base.SetupServices(services);
    }
}
