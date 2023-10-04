using Eventuous.EventStore.Subscriptions;
using Eventuous.Projections.MongoDB;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Tests.Projections.MongoDB;

public class ProjectionTestBase<TProjection> : IClassFixture<IntegrationFixture>, IAsyncLifetime where TProjection : class, IEventHandler {
    protected readonly IntegrationFixture Fixture;
    protected readonly IHost              Host;

    protected ProjectionTestBase(string id, IntegrationFixture fixture, ITestOutputHelper output) {
        Fixture = fixture;

        var builder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureLogging(cfg => cfg.AddXunit(output, LogLevel.Debug).SetMinimumLevel(LogLevel.Trace))
            .ConfigureServices(collection => ConfigureServices(collection, id));

        Host = builder.Build();
        Host.UseEventuousLogs();
    }

    void ConfigureServices(IServiceCollection services, string id)
        => services
            .AddSingleton(Fixture.Client)
            .AddSingleton(Fixture.Mongo)
            .AddCheckpointStore<MongoCheckpointStore>()
            .AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
                id,
                builder => builder.AddEventHandler<TProjection>()
            );

    protected string CreateId() => new(Guid.NewGuid().ToString("N"));

    protected async Task WaitForPosition(ulong position) {
        var checkpointStore = Host.Services.GetRequiredService<ICheckpointStore>();
        var count = 100;

        while (count-- > 0) {
            var checkpoint = await checkpointStore.GetLastCheckpoint(nameof(ProjectWithBuilder), default);

            if (checkpoint.Position.HasValue && checkpoint.Position.Value >= position) break;

            await Task.Delay(100);
        }
    }

    public Task InitializeAsync()
        => Host.StartAsync();

    public Task DisposeAsync()
        => Host.StopAsync();
}
