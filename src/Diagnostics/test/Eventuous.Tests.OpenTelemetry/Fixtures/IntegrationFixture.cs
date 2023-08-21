using System.Text.Json;
using EventStore.Client;
using Eventuous.Diagnostics.Tracing;
using Eventuous.EventStore;
using NodaTime.Serialization.SystemTextJson;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.OpenTelemetry.Fixtures;

public class IntegrationFixture : IAsyncLifetime {
    public IEventStore      EventStore     { get; set; }         = null!;
    public IAggregateStore  AggregateStore { get; set; }         = null!;
    public EventStoreClient Client         { get; private set; } = null!;
    public Fixture          Auto           { get; }              = new();

    EventStoreDbContainer _esdbContainer = null!;
    // readonly ActivityListener _listener = DummyActivityListener.Create();

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public IntegrationFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        // ActivitySource.AddActivityListener(_listener);
    }

    public virtual async Task InitializeAsync() {
        _esdbContainer = new EventStoreDbBuilder()
            .WithImage("eventstore/eventstore:22.10.2-alpha-arm64v8")
            .Build();
        await _esdbContainer.StartAsync();
        var settings = EventStoreClientSettings.Create(_esdbContainer.GetConnectionString());
        Client         = new EventStoreClient(settings);
        EventStore     = new TracedEventStore(new EsdbEventStore(Client));
        AggregateStore = new AggregateStore(EventStore);
    }

    public async Task DisposeAsync() {
        await Client.DisposeAsync();
        await _esdbContainer.DisposeAsync();
    }
}
