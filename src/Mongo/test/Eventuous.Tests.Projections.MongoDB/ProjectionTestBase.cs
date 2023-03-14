using Eventuous.EventStore.Subscriptions;
using Eventuous.Projections.MongoDB;
using Eventuous.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Eventuous.Tests.Projections.MongoDB.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Projections.MongoDB; 

public class ProjectionTestBase<TProjection> : IAsyncLifetime where TProjection : class, IEventHandler {
    readonly IHost _host;

    protected ProjectionTestBase(string id,  ITestOutputHelper output) {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureLogging(cfg => cfg.AddXunit(output, LogLevel.Debug).SetMinimumLevel(LogLevel.Trace))
            .ConfigureServices(collection => ConfigureServices(collection, id));

        _host = builder.Build();
        _host.UseEventuousLogs();
    }

    static void ConfigureServices(IServiceCollection services, string id)
        => services
            .AddSingleton(Instance.Client)
            .AddSingleton(Instance.Mongo)
            .AddCheckpointStore<MongoCheckpointStore>()
            .AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
                id,
                builder => builder.AddEventHandler<TProjection>()
            );

    protected string CreateId() => new(Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
        => _host.StartAsync();

    public Task DisposeAsync()
        => _host.StopAsync();
}
