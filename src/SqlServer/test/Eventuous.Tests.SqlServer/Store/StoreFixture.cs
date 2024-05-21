using Eventuous.SqlServer;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.SqlServer.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.SqlEdge;

namespace Eventuous.Tests.SqlServer.Store;

public sealed class StoreFixture : StoreFixtureBase<SqlEdgeContainer> {
    readonly string _schemaName = Faker.Internet.UserName().Replace(".", "_").Replace("-", "").Replace(" ", "").ToLower();

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousSqlServer(Container.GetConnectionString(), _schemaName, true);
        services.AddAggregateStore<SqlServerStore>();
    }

    protected override SqlEdgeContainer CreateContainer() => SqlContainer.Create();
}
