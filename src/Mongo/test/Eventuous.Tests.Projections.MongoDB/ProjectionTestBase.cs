using Eventuous.EventStore.Subscriptions;
using Eventuous.Projections.MongoDB;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Tests.Projections.MongoDB;

public abstract class ProjectionTestBase {
    readonly  string       _id;
    protected IHost        Host = null!;
    readonly  IHostBuilder _builder;

    protected ProjectionTestBase(string id) {
        _id      = id;
        _builder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().ConfigureLogging(cfg => cfg.ForTests());
    }

    protected abstract void ConfigureServices(IServiceCollection services, string id);

    [Before(Test)]
    public async Task InitializeAsync() {
        _builder.ConfigureServices(collection => ConfigureServices(collection, _id));
        Host = _builder.Build();
        Host.Services.AddEventuousLogs();
        await Host.StartAsync();
    }

    [After(Test)]
    public async Task DisposeAsync() => await Host.StopAsync();
}

public abstract class ProjectionTestBase<TProjection>(string id, IntegrationFixture fixture) : ProjectionTestBase(id)
    where TProjection : class, IEventHandler {
    protected readonly IntegrationFixture Fixture = fixture;

    protected override void ConfigureServices(IServiceCollection services, string id)
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
        var count           = 100;

        while (count-- > 0) {
            var checkpoint = await checkpointStore.GetLastCheckpoint(nameof(ProjectWithBuilder), default);

            if (checkpoint.Position.HasValue && checkpoint.Position.Value >= position) break;

            await Task.Delay(100);
        }
    }
}
