using Eventuous.AspNetCore.Web;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Projections.MongoDB;
using Eventuous.Subscriptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static Eventuous.Tests.Projections.MongoDB.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Projections.MongoDB; 

public class ProjectionTestBase<TProjection> : IDisposable where TProjection : class, IEventHandler {
    readonly TestServer _host;

    protected ProjectionTestBase(string id,  ITestOutputHelper output) {
        var builder = new WebHostBuilder()
            .ConfigureLogging(cfg => cfg.AddXunit(output, LogLevel.Debug).SetMinimumLevel(LogLevel.Debug))
            .ConfigureServices(collection => ConfigureServices(collection, id))
            .Configure(x => x.UseEventuousLogs());

        _host = new TestServer(builder);
    }

    void ConfigureServices(IServiceCollection services, string id)
        => services
            .AddSingleton(Instance.Client)
            .AddSingleton(Instance.Mongo)
            .AddCheckpointStore<MongoCheckpointStore>()
            .AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
                id,
                builder => builder.AddEventHandler<TProjection>()
            );

    public void Dispose() => _host.Dispose();
    
    protected string CreateId() => new(Guid.NewGuid().ToString("N"));
}
