using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.KurrentDB;
using Eventuous.Tests.Persistence.Base.Fixtures;
using KurrentDB.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.KurrentDB.Fixtures;

public class StoreFixture : StoreFixtureBase<EventStoreDbContainer> {
    public KurrentDBClient Client { get; private set; } = null!;
#pragma warning disable CS0618 // Type or member is obsolete
    public IAggregateStore AggregateStore { get; private set; } = null!;
#pragma warning restore CS0618 // Type or member is obsolete

    readonly ActivityListener _listener = DummyActivityListener.Create();

    // static StoreFixture() => AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http2FlowControl.DisableDynamicWindowSizing", true);

    public StoreFixture() : this(LogLevel.Information) { }

    public StoreFixture(LogLevel logLevel) : base(logLevel) {
        ActivitySource.AddActivityListener(_listener);
    }

    protected override void SetupServices(IServiceCollection services) {
        services.AddKurrentDBClient(Container.GetConnectionString());
        services.AddEventStore<EsdbEventStore>();
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddSingleton<IAggregateStore, AggregateStore>();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    protected override EventStoreDbContainer CreateContainer() => EsdbContainer.Create();

    protected override void GetDependencies(IServiceProvider provider) {
        Client = provider.GetRequiredService<KurrentDBClient>();
#pragma warning disable CS0618 // Type or member is obsolete
        AggregateStore = Provider.GetRequiredService<IAggregateStore>();
#pragma warning restore CS0618 // Type or member is obsolete
        provider.AddEventuousLogs();
    }
}
