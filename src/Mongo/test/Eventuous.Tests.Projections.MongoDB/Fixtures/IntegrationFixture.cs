using System.Text.Json;
using EventStore.Client;
using Eventuous.EventStore;
using MongoDb.Bson.NodaTime;
using MongoDB.Driver;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Eventuous.Tests.Projections.MongoDB.Fixtures;

public sealed class IntegrationFixture : IAsyncDisposable {
    public IEventStore      EventStore     { get; }
    public IAggregateStore  AggregateStore { get; }
    public EventStoreClient Client         { get; }
    public IMongoDatabase   Mongo          { get; }
    public Fixture          Auto           { get; } = new();

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public static IntegrationFixture Instance { get; } = new();

    public Task<AppendEventsResult> AppendEvent(
        StreamName             streamName,
        object                 evt,
        ExpectedStreamVersion? version = null
    )
        => EventStore.AppendEvents(
            streamName,
            version ?? ExpectedStreamVersion.Any,
            new[] {
                new StreamEvent(Guid.NewGuid(), evt, new Metadata(), "application/json", 0)
            },
            CancellationToken.None
        );

    IntegrationFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
        Client         = new EventStoreClient(settings);
        EventStore     = new EsdbEventStore(Client);
        AggregateStore = new AggregateStore(EventStore);
        Mongo          = ConfigureMongo();
    }

    static IMongoDatabase ConfigureMongo() {
        NodaTimeSerializers.Register();
        var settings = MongoClientSettings.FromConnectionString("mongodb://mongoadmin:secret@localhost:27017");
        return new MongoClient(settings).GetDatabase("bookings");
    }

    public ValueTask DisposeAsync() => Client.DisposeAsync();
}
