using Eventuous.SqlServer;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.SqlServer.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Eventuous.Tests.SqlServer.Store;

public sealed class StoreFixture : StoreFixtureBase<MsSqlContainer> {
    readonly string _schemaName = GetSchemaName();

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousSqlServer(Container.GetConnectionString(), _schemaName, true);
        services.AddEventStore<SqlServerStore>();
    }

    protected override MsSqlContainer CreateContainer() => SqlContainer.Create();
}
