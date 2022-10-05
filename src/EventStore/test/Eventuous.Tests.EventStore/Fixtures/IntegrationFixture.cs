using System.Diagnostics;
using System.Text.Json;
using EventStore.Client;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Tracing;
using Eventuous.EventStore;
using MicroElements.AutoFixture.NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Eventuous.Tests.EventStore.Fixtures;

public sealed class IntegrationFixture : IAsyncDisposable {
    public IEventStore      EventStore     { get; }
    public IAggregateStore  AggregateStore { get; }
    public EventStoreClient Client         { get; }
    public IFixture          Auto           { get; } = new Fixture().Customize(new NodaTimeCustomization());

    readonly ActivityListener _listener = DummyActivityListener.Create();

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
        ActivitySource.AddActivityListener(_listener);
    }

    public async ValueTask DisposeAsync() {
        _listener.Dispose();
        await Client.DisposeAsync();
    }
}
