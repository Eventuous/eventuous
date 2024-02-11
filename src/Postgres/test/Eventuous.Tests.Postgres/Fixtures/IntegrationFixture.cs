using Bogus;
using Eventuous.Postgresql;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Fixtures;

public class IntegrationFixture : StoreFixtureBase<PostgreSqlContainer> {
    protected NpgsqlDataSource DataSource { get; private set; } = null!;

    protected readonly string SchemaName = new Faker().Internet.UserName().Replace(".", "_").Replace("-", "").Replace(" ", "").ToLower();

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousPostgres(Container.GetConnectionString(), SchemaName, true);
        services.AddAggregateStore<PostgresStore>();
    }

    protected override void GetDependencies(IServiceProvider provider) {
        DataSource = provider.GetRequiredService<NpgsqlDataSource>();
    }

    protected override PostgreSqlContainer CreateContainer() => PostgresContainer.Create();
}
