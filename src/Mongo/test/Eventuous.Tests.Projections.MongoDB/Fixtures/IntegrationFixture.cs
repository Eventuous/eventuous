using System.Runtime.InteropServices;
using Eventuous.KurrentDB;
using Eventuous.TestHelpers;
using KurrentDB.Client;
using MongoDb.Bson.NodaTime;
using MongoDB.Driver;
using Testcontainers.KurrentDb;
using Testcontainers.MongoDb;
using TUnit.Core.Interfaces;

namespace Eventuous.Tests.Projections.MongoDB.Fixtures;

public sealed class IntegrationFixture : IAsyncInitializer, IAsyncDisposable {
    public IEventStore     EventStore { get; set; }         = null!;
    public KurrentDBClient Client     { get; private set; } = null!;
    public IMongoDatabase  Mongo      { get; private set; } = null!;

    static IEventSerializer Serializer { get; } = new DefaultEventSerializer(TestPrimitives.DefaultOptions);

    public Task<AppendEventsResult> AppendEvent(StreamName streamName, object evt, ExpectedStreamVersion? version = null)
        => EventStore.AppendEvents(
            streamName,
            version ?? ExpectedStreamVersion.Any,
            [new(Guid.NewGuid(), evt, new())],
            CancellationToken.None
        );

    static IntegrationFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        NodaTimeSerializers.Register();
    }

    KurrentDbContainer _esdbContainer  = null!;
    MongoDbContainer   _mongoContainer = null!;

    public async Task InitializeAsync() {
        var image = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "kurrentplatform/kurrentdb:25.1.3-experimental-arm64-8.0-jammy"
            : "kurrentplatform/kurrentdb:25.1.3";
        _esdbContainer = new KurrentDbBuilder().WithImage(image).Build();
        await _esdbContainer.StartAsync();
        var settings = KurrentDBClientSettings.Create(_esdbContainer.GetConnectionString());
        Client          = new(settings);
        EventStore      = new KurrentDBEventStore(Client);
        _mongoContainer = new MongoDbBuilder().WithImage("mongo:8").Build();
        await _mongoContainer.StartAsync();
        var mongoSettings = MongoClientSettings.FromConnectionString(_mongoContainer.GetConnectionString());
        Mongo = new MongoClient(mongoSettings).GetDatabase("bookings");
    }

    public async ValueTask DisposeAsync() {
        await Client.DisposeAsync();
        await _esdbContainer.DisposeAsync();
    }
}
