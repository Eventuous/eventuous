using Azure.Storage.Blobs;
using Testcontainers.Azurite;
using TUnit.Core.Interfaces;

namespace Eventuous.Tests.Azure.Storage.Blobs.Fixtures;

public sealed class IntegrationFixture : IAsyncInitializer, IAsyncDisposable {
    public BlobServiceClient BlobServiceClient { get; private set; } = null!;

    AzuriteContainer _azuriteContainer = null!;

    public async Task InitializeAsync() {
        // Start Azurite container for blob storage
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithCommand("--skipApiVersionCheck")
            .Build();
        await _azuriteContainer.StartAsync();

        BlobServiceClient = new BlobServiceClient(_azuriteContainer.GetConnectionString());
    }

    public async ValueTask DisposeAsync() => await _azuriteContainer.DisposeAsync();
}
