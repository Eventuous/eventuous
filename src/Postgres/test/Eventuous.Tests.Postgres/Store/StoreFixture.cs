using Eventuous.Postgresql;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Postgres.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Store;

// ReSharper disable once PartialTypeWithSinglePart
public partial class StoreFixture : StoreFixtureBase<PostgreSqlContainer> {
    protected NpgsqlDataSource DataSource { get; private set; } = null!;

    readonly string _schemaName = GetSchemaName();

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousPostgres(Container.GetConnectionString(), _schemaName, true);
        services.AddEventStore<PostgresStore>();
    }

    protected override void GetDependencies(IServiceProvider provider) {
        DataSource = provider.GetRequiredService<NpgsqlDataSource>();
    }

    protected override PostgreSqlContainer CreateContainer() => PostgresContainer.Create();
}
