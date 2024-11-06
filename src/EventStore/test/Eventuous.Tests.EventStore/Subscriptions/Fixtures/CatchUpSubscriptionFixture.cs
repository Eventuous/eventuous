using EventStore.Client;
using Eventuous.EventStore;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Subscriptions.Fixtures;

public class CatchUpSubscriptionFixture<TSubscription, TSubscriptionOptions, TEventHandler>(
        Action<TSubscriptionOptions> configureOptions,
        StreamName                   streamName,
        bool                         autoStart         = true,
        Action<IServiceCollection>?  configureServices = null,
        LogLevel                     logLevel          = LogLevel.Debug
    ) : SubscriptionFixtureBase<EventStoreDbContainer, TSubscription, TSubscriptionOptions, TestCheckpointStore, TEventHandler>(autoStart, logLevel)
    where TSubscription : EventStoreCatchUpSubscriptionBase<TSubscriptionOptions>
    where TSubscriptionOptions : CatchUpSubscriptionOptions
    where TEventHandler : class, IEventHandler {
    protected override EventStoreDbContainer CreateContainer() => EsdbContainer.Create();

    protected override TestCheckpointStore GetCheckpointStore(IServiceProvider sp) => new();

    protected override void ConfigureSubscription(TSubscriptionOptions options) => configureOptions(options);

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);
        services.AddEventStoreClient(Container.GetConnectionString());
        services.AddEventStore<EsdbEventStore>();
        services.AddSingleton(new TestEventHandlerOptions());
        configureServices?.Invoke(services);
    }

    protected override void GetDependencies(IServiceProvider provider) {
        base.GetDependencies(provider);
        Client = provider.GetRequiredService<EventStoreClient>();
    }

    public EventStoreClient Client { get; set; } = null!;

    protected override ILoggingBuilder ConfigureLogging(ILoggingBuilder builder) => base.ConfigureLogging(builder).AddFilter(Filter);

    public override async Task<ulong> GetLastPosition() {
        return streamName == "$all" ? await GetLastFromAll() : await GetLastFromStream();

        async Task<ulong> GetLastFromStream() {
            var lastEvent = await Client.ReadStreamAsync(Direction.Backwards, streamName, StreamPosition.End, 1).ToArrayAsync();

            return lastEvent.Length == 0 ? 0 : lastEvent[0].Event.Position.CommitPosition;
        }

        async Task<ulong> GetLastFromAll() {
            var lastEvent = await Client.ReadAllAsync(Direction.Backwards, Position.End, 1).ToArrayAsync();

            return lastEvent.Length == 0 ? 0 : lastEvent[0].Event.Position.CommitPosition;
        }
    }

    // ReSharper disable once StaticMemberInGenericType
    static readonly string[] Categories = [
        // "EventStore.Client.SharingProvider",
        "Grpc.Net.Client.Internal.GrpcCall"
    ];

    static bool Filter(string? provider, string? category, LogLevel logLevel) {
        if (category == null) return true;

        return !Categories.Contains(category);
    }
}
