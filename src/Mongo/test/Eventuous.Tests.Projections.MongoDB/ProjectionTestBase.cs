using Eventuous.EventStore.Subscriptions;
using Eventuous.Projections.MongoDB;
using Eventuous.Subscriptions;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Tests.Projections.MongoDB;

public class ProjectionTestBase<TProjection> : IClassFixture<IntegrationFixture>, IAsyncLifetime where TProjection : class, IEventHandler {
    readonly IHost              _host;
    readonly IntegrationFixture _fixture;

    protected ProjectionTestBase(string id, IntegrationFixture fixture, ITestOutputHelper output) {
        _fixture = fixture;

        var builder = Host.CreateDefaultBuilder()
            .ConfigureLogging(cfg => cfg.AddXunit(output, LogLevel.Debug).SetMinimumLevel(LogLevel.Trace))
            .ConfigureServices(collection => ConfigureServices(collection, id));

        _host = builder.Build();
        _host.UseEventuousLogs();
    }

    void ConfigureServices(IServiceCollection services, string id)
        => services
            .AddSingleton(_fixture.Client)
            .AddSingleton(_fixture.Mongo)
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
