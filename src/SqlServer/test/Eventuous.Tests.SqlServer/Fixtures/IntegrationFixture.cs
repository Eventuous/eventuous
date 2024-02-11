using Eventuous.SqlServer;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection1;
using Testcontainers.SqlEdge;

namespace Eventuous.Tests.SqlServer.Fixtures;

public sealed class IntegrationFixture : StoreFixtureBase<SqlEdgeContainer> {
    public string SchemaName { get; } = Faker.Internet.UserName().Replace(".", "_").Replace("-", "").Replace(" ", "").ToLower();

    public string GetConnectionString() => Container.GetConnectionString();

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousSqlServer(Container.GetConnectionString(), SchemaName, true);
        services.AddAggregateStore<SqlServerStore>();
    }

    protected override SqlEdgeContainer CreateContainer() => SqlContainer.Create();
}
