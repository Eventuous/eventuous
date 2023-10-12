using System.Text.Json;
using EventStore.Client;
using Eventuous.Diagnostics.Tracing;
using Eventuous.EventStore;
using NodaTime.Serialization.SystemTextJson;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.OpenTelemetry.Fixtures;

public class IntegrationFixture : IAsyncLifetime {
    public IEventStore      EventStore { get; set; }         = null!;
    public EventStoreClient Client     { get; private set; } = null!;
    public Fixture          Auto       { get; }              = new();

    EventStoreDbContainer _esdbContainer = null!;

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public IntegrationFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        // ActivitySource.AddActivityListener(_listener);
    }

    public virtual async Task InitializeAsync() {
        _esdbContainer = new EventStoreDbBuilder().Build();
        await _esdbContainer.StartAsync();
        var settings = EventStoreClientSettings.Create(_esdbContainer.GetConnectionString());
        Client         = new EventStoreClient(settings);
        EventStore     = new TracedEventStore(new EsdbEventStore(Client));
        new AggregateStore(EventStore);
    }

    public async Task DisposeAsync() {
        await Client.DisposeAsync();
        await _esdbContainer.DisposeAsync();
    }
}
