using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Projections.MongoDB;
using Eventuous.Subscriptions;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using Eventuous.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using static Microsoft.Extensions.Hosting.Host;

namespace Eventuous.Tests.Projections.MongoDB;

public abstract class ProjectionTestBase {
    protected string SubscriptionId { get; }
    readonly IHostBuilder _builder;

    protected IHost Host = null!;

    protected ProjectionTestBase(string subscriptionId) {
        SubscriptionId = subscriptionId;
        _builder = CreateDefaultBuilder().ConfigureLogging(cfg => cfg.ForTests());
    }

    protected abstract void ConfigureServices(IServiceCollection services, string subscriptionId);

    public async Task InitializeAsync() {
        _builder.ConfigureServices(collection => ConfigureServices(collection, SubscriptionId));
        Host = _builder.Build();
        Host.Services.AddEventuousLogs();
        await Host.StartAsync();
    }

    public async Task DisposeAsync() => await Host.StopAsync();
}

public class ProjectionTestBase<TProjection>(string subscriptionId, IntegrationFixture fixture) : ProjectionTestBase(subscriptionId)
    where TProjection : class, IEventHandler {
    public readonly IntegrationFixture Fixture = fixture;

    protected override void ConfigureServices(IServiceCollection services, string subscriptionId)
        => services
            .AddSingleton(Fixture.Client)
            .AddSingleton(Fixture.Mongo)
            .AddCheckpointStore<MongoCheckpointStore>()
            .AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
                subscriptionId,
                builder => builder.AddEventHandler<TProjection>()
            );

    public string CreateId() => new(Guid.NewGuid().ToString("N"));

    public async Task WaitForPosition(ulong position, CancellationToken cancellationToken) {
        var options = Host.Services.GetRequiredService<IOptions<MongoCheckpointStoreOptions>>().Value;
        var checkpoints = Fixture.Mongo.GetCollection<StoredCheckpoint>(options.CollectionName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        ulong? observedPosition = null;

        try {
            while (true) {
                // GetLastCheckpoint initializes the store's write subject. Poll storage without replacing the active writer.
                var checkpoint = await checkpoints.AsQueryable()
                    .Where(x => x.Id == SubscriptionId)
                    .SingleOrDefaultAsync(cts.Token)
                    .NoContext();
                observedPosition = checkpoint?.Position;

                if (observedPosition.HasValue && observedPosition.Value >= position) return;

                await Task.Delay(100, cts.Token).NoContext();
            }
        } catch (OperationCanceledException ex) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            throw new TimeoutException(
                $"Expected subscription '{SubscriptionId}' to reach checkpoint {position} within 30 seconds, but observed {observedPosition?.ToString() ?? "no checkpoint"}.",
                ex
            );
        }
    }

    record StoredCheckpoint(string Id, ulong? Position);
}
