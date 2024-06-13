using System.Text.RegularExpressions;
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

    readonly string _schemaName = NormaliseRegex().Replace(Faker.Internet.UserName(), "").ToLower();

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousPostgres(Container.GetConnectionString(), _schemaName, true);
        services.AddAggregateStore<PostgresStore>();
    }

    protected override void GetDependencies(IServiceProvider provider) {
        DataSource = provider.GetRequiredService<NpgsqlDataSource>();
    }

    protected override PostgreSqlContainer CreateContainer() => PostgresContainer.Create();

#if NET8_0_OR_GREATER
    [GeneratedRegex(@"[\.\-\s]")]
    private static partial Regex NormaliseRegex();
#else
    static Regex NormaliseRegex() => new(@"[\.\-\s]");
#endif
}
