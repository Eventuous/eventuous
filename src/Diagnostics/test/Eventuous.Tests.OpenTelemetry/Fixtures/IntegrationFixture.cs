using System.Text.Json;
using EventStore.Client;
using Eventuous.Diagnostics.Tracing;
using Eventuous.EventStore;
using NodaTime.Serialization.SystemTextJson;

namespace Eventuous.Tests.OpenTelemetry.Fixtures;

public class IntegrationFixture : IDisposable {
    public IEventStore      EventStore     { get; }
    public IAggregateStore  AggregateStore { get; }
    public EventStoreClient Client         { get; }
    public Fixture          Auto           { get; } = new();

    // readonly ActivityListener _listener = DummyActivityListener.Create();

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public static IntegrationFixture Instance { get; } = new();

    IntegrationFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
        Client         = new EventStoreClient(settings);
        EventStore     = new TracedEventStore(new EsdbEventStore(Client));
        AggregateStore = new AggregateStore(EventStore);
        // ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() {
        // _listener.Dispose();
        Client.Dispose();
    }
}