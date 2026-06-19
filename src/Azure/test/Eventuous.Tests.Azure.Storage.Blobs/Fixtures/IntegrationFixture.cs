using System.Runtime.InteropServices;
using Azure.Storage.Blobs;
using Eventuous.KurrentDB;
using Eventuous.TestHelpers;
using KurrentDB.Client;
using Testcontainers.Azurite;
using Testcontainers.KurrentDb;
using TUnit.Core.Interfaces;

namespace Eventuous.Tests.Azure.Storage.Blobs.Fixtures;

public sealed class IntegrationFixture : IAsyncInitializer, IAsyncDisposable {
    public IEventStore EventStore { get; set; } = null!;
    public BlobServiceClient BlobServiceClient { get; private set; } = null!;
    public KurrentDBClient Client { get; private set; } = null!;

    static IEventSerializer Serializer { get; } = new DefaultEventSerializer(TestPrimitives.DefaultOptions);

    AzuriteContainer _azuriteContainer = null!;
    KurrentDbContainer _esdbContainer = null!;

    public async Task<AppendEventsResult> AppendEvent(
        StreamName streamName,
        object evt,
        ExpectedStreamVersion? version = null
    ) {
        return await EventStore.AppendEvents(
            streamName,
            version ?? ExpectedStreamVersion.Any,
            [new(Guid.NewGuid(), evt, new())],
            CancellationToken.None
        );
    }

    static IntegrationFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
    }

    public async Task InitializeAsync() {
        // Start Azurite container for blob storage
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithCommand("--skipApiVersionCheck")
            .Build();
        await _azuriteContainer.StartAsync();

        var connectionString = _azuriteContainer.GetConnectionString();
        BlobServiceClient = new BlobServiceClient(connectionString);

        // Start KurrentDB for event store
        var image = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "kurrentplatform/kurrentdb:25.1.3-experimental-arm64-8.0-jammy"
            : "kurrentplatform/kurrentdb:25.1.3";
        _esdbContainer = new KurrentDbBuilder()
            .WithImage(image)
            .Build();
        await _esdbContainer.StartAsync();
        var settings = KurrentDBClientSettings.Create(_esdbContainer.GetConnectionString());
        Client = new(settings);
        EventStore = new KurrentDBEventStore(Client);
    }

    public async ValueTask DisposeAsync() {
        await Client.DisposeAsync();
        await _esdbContainer.DisposeAsync();
        await _azuriteContainer.DisposeAsync();
    }
}
