using System.Diagnostics;
using EventStore.Client;
using Eventuous.Diagnostics;
using Eventuous.EventStore;
using Eventuous.TestHelpers;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Fixtures;

public class StoreFixture : StoreFixtureBase<EventStoreDbContainer> {
    public EventStoreClient Client { get; private set; } = null!;
#pragma warning disable CS0618 // Type or member is obsolete
    public IAggregateStore AggregateStore { get; private set; } = null!;
#pragma warning restore CS0618 // Type or member is obsolete

    readonly ActivityListener _listener = DummyActivityListener.Create();

    static StoreFixture() {
        AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http2FlowControl.DisableDynamicWindowSizing", true);
    }

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(TestPrimitives.DefaultOptions);

    public StoreFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        ActivitySource.AddActivityListener(_listener);
    }

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventStoreClient(Container.GetConnectionString());
        services.AddEventStore<EsdbEventStore>();
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddSingleton<IAggregateStore, AggregateStore>();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    protected override EventStoreDbContainer CreateContainer() => EsdbContainer.Create();

    protected override void GetDependencies(IServiceProvider provider) {
        Client         = provider.GetRequiredService<EventStoreClient>();
#pragma warning disable CS0618 // Type or member is obsolete
        AggregateStore = Provider.GetRequiredService<IAggregateStore>();
#pragma warning restore CS0618 // Type or member is obsolete
        provider.AddEventuousLogs();
    }
}
