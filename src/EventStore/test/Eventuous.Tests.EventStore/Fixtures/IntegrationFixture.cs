using System.Diagnostics;
using System.Text.Json;
using EventStore.Client;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Tracing;
using Eventuous.EventStore;
using MicroElements.AutoFixture.NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Fixtures;

public sealed class IntegrationFixture : IAsyncLifetime {
    public IEventStore      EventStore     { get; private set; } = null!;
    public IAggregateStore  AggregateStore { get; private set; } = null!;
    public EventStoreClient Client         { get; private set; } = null!;
    public IFixture         Auto           { get; }              = new Fixture().Customize(new NodaTimeCustomization());

    readonly ActivityListener _listener = DummyActivityListener.Create();

    EventStoreDbContainer _esdbContainer = null!;

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public IntegrationFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        ActivitySource.AddActivityListener(_listener);
    }

    public async Task InitializeAsync() {
        _esdbContainer = new EventStoreDbBuilder().Build();
        await _esdbContainer.StartAsync();
        var settings = EventStoreClientSettings.Create(_esdbContainer.GetConnectionString());
        Client         = new EventStoreClient(settings);
        EventStore     = new TracedEventStore(new EsdbEventStore(Client));
        AggregateStore = new AggregateStore(EventStore);
    }

    public async Task DisposeAsync() {
        _listener.Dispose();

        try {
            await Client.DisposeAsync();
            await _esdbContainer.DisposeAsync();
        } catch (Exception e) {
            Console.WriteLine(e);
        }
    }
}
