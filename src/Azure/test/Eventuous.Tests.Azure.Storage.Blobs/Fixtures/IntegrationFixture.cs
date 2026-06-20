using Azure.Storage.Blobs;
using Eventuous.TestHelpers;
using Testcontainers.Azurite;
using TUnit.Core.Interfaces;

namespace Eventuous.Tests.Azure.Storage.Blobs.Fixtures;

public sealed class IntegrationFixture : IAsyncInitializer, IAsyncDisposable {
    public IEventStore EventStore { get; set; } = null!;
    public BlobServiceClient BlobServiceClient { get; private set; } = null!;

    static IEventSerializer Serializer { get; } = new DefaultEventSerializer(TestPrimitives.DefaultOptions);

    AzuriteContainer _azuriteContainer = null!;

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

    }

    public async ValueTask DisposeAsync() {
        await _azuriteContainer.DisposeAsync();
    }
}
