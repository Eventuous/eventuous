using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Eventuous.Azure.Storage.Blobs;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Tests.Azure.Storage.Blobs.Fixtures;
using RequestFailedException = Azure.RequestFailedException;

namespace Eventuous.Tests.Azure.Storage.Blobs;

[ClassDataSource<IntegrationFixture>]
public class StorageBlobsProjectorTests(IntegrationFixture fixture) {
    [Test]
    public async Task On_SyncStateHandler_ShouldHandleNewBlob() {
        // Arrange
        var containerName = "test-sync-state-new";
        var containerClient = fixture.BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateAsync();

        var projector = new TestProjectorWithSyncStateHandler(containerClient);
        var context = CreateContext(fixture, new TestEvent { Value = 10 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await Assert.That(result).IsEqualTo(EventHandlingStatus.Success);

        var blobClient = containerClient.GetBlobClient("test-stream/SyncState.json");
        var blob = await blobClient.DownloadContentAsync();
        var state = JsonSerializer.Deserialize<SyncState>(blob.Value.Content.ToString())!;

        await Assert.That(state.Value).IsEqualTo(10);
    }

    [Test]
    public async Task On_SyncStateHandler_ShouldUpdateExistingBlob() {
        // Arrange
        var containerName = "test-sync-state-existing";
        var containerClient = fixture.BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateAsync();

        // Create initial blob
        var blobClient = containerClient.GetBlobClient("test-stream/SyncState.json");
        var initialState = new SyncState { Value = 5 };
        var json = JsonSerializer.SerializeToUtf8Bytes(initialState);
        await blobClient.UploadAsync(new MemoryStream(json), overwrite: true);

        var projector = new TestProjectorWithSyncStateHandler(containerClient);
        var context = CreateContext(fixture, new TestEvent { Value = 10 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await Assert.That(result).IsEqualTo(EventHandlingStatus.Success);

        var blob = await blobClient.DownloadContentAsync();
        var state = JsonSerializer.Deserialize<SyncState>(blob.Value.Content.ToString())!;

        await Assert.That(state.Value).IsEqualTo(15); // 5 + 10
        await Assert.That(state.Counter).IsEqualTo(1);
    }

    [Test]
    public async Task On_SyncContextStateHandler_ShouldHandleNewBlob() {
        // Arrange
        var containerName = "test-sync-context-new";
        var containerClient = fixture.BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateAsync();

        var projector = new TestProjectorWithSyncContextStateHandler(containerClient);
        var context = CreateContext(fixture, new TestEvent { Value = 20 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await Assert.That(result).IsEqualTo(EventHandlingStatus.Success);

        var blobClient = containerClient.GetBlobClient("test-stream/SyncContextState.json");
        var blob = await blobClient.DownloadContentAsync();
        var state = JsonSerializer.Deserialize<SyncContextState>(blob.Value.Content.ToString())!;

        await Assert.That(state.Value).IsEqualTo(20);
        await Assert.That(state.StreamId).IsEqualTo("test-stream");
    }

    [Test]
    public async Task On_AsyncStateHandler_ShouldHandleNewBlob() {
        // Arrange
        var containerName = "test-async-state-new";
        var containerClient = fixture.BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateAsync();

        var projector = new TestProjectorWithAsyncStateHandler(containerClient);
        var context = CreateContext(fixture, new TestEvent { Value = 30 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await Assert.That(result).IsEqualTo(EventHandlingStatus.Success);

        var blobClient = containerClient.GetBlobClient("test-stream/AsyncState.json");
        var blob = await blobClient.DownloadContentAsync();
        var state = JsonSerializer.Deserialize<AsyncState>(blob.Value.Content.ToString())!;

        await Assert.That(state.Value).IsEqualTo(30);
    }

    [Test]
    public async Task On_AsyncStateHandler_ShouldUpdateExistingBlob() {
        // Arrange
        var containerName = "test-async-state-existing";
        var containerClient = fixture.BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateAsync();

        // Create initial blob
        var blobClient = containerClient.GetBlobClient("test-stream/AsyncState.json");
        var initialState = new AsyncState { Value = 5 };
        var json = JsonSerializer.SerializeToUtf8Bytes(initialState);
        await blobClient.UploadAsync(new MemoryStream(json), overwrite: true);

        var projector = new TestProjectorWithAsyncStateHandler(containerClient);
        var context = CreateContext(fixture, new TestEvent { Value = 35 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await Assert.That(result).IsEqualTo(EventHandlingStatus.Success);

        var blob = await blobClient.DownloadContentAsync();
        var state = JsonSerializer.Deserialize<AsyncState>(blob.Value.Content.ToString())!;

        await Assert.That(state.Value).IsEqualTo(40); // 5 + 35
    }

    [Test]
    public async Task On_AsyncContextStateHandler_ShouldHandleNewBlob() {
        // Arrange
        var containerName = "test-async-context-new";
        var containerClient = fixture.BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateAsync();

        var projector = new TestProjectorWithAsyncContextStateHandler(containerClient);
        var context = CreateContext(fixture, new TestEvent { Value = 40, Name = "AsyncContext" });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await Assert.That(result).IsEqualTo(EventHandlingStatus.Success);

        var blobClient = containerClient.GetBlobClient("test-stream/AsyncContextState.json");
        var blob = await blobClient.DownloadContentAsync();
        var state = JsonSerializer.Deserialize<AsyncContextState>(blob.Value.Content.ToString())!;

        await Assert.That(state.Value).IsEqualTo(40);
        await Assert.That(state.EventName).IsEqualTo("AsyncContext");
    }

    [Test]
    public async Task On_AsyncContextStateHandler_ShouldUpdateExistingBlob() {
        // Arrange
        var containerName = "test-async-context-existing";
        var containerClient = fixture.BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateAsync();

        // Create initial blob
        var blobClient = containerClient.GetBlobClient("test-stream/AsyncContextState.json");
        var initialState = new AsyncContextState { Value = 10, EventName = "Initial" };
        var json = JsonSerializer.SerializeToUtf8Bytes(initialState);
        await blobClient.UploadAsync(new MemoryStream(json), overwrite: true);

        var projector = new TestProjectorWithAsyncContextStateHandler(containerClient);
        var context = CreateContext(fixture, new TestEvent { Value = 50, Name = "Update" });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await Assert.That(result).IsEqualTo(EventHandlingStatus.Success);

        var blob = await blobClient.DownloadContentAsync();
        var state = JsonSerializer.Deserialize<AsyncContextState>(blob.Value.Content.ToString())!;

        await Assert.That(state.Value).IsEqualTo(60); // 10 + 50
        await Assert.That(state.EventName).IsEqualTo("Update");
    }

    [Test]
    public async Task HandleEvent_NoHandler_ShouldReturnIgnored() {
        // Arrange
        var containerName = "test-no-handler";
        var containerClient = fixture.BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateAsync();

        var projector = new TestProjectorNoHandler(containerClient);
        var context = CreateContext(fixture, new TestEvent { Value = 100 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await Assert.That(result).IsEqualTo(EventHandlingStatus.Ignored);
    }

    [Test]
    public async Task HandleInternal_ConcurrentModification_ShouldReturnIgnored() {
        // Arrange
        var containerName = "test-concurrent";
        var containerClient = fixture.BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateAsync();

        // Create initial blob
        var blobClient = containerClient.GetBlobClient("concurrent-stream/ConcurrentState.json");
        var initialState = new ConcurrentState { Value = 1 };
        var json = JsonSerializer.SerializeToUtf8Bytes(initialState);
        await blobClient.UploadAsync(new MemoryStream(json), overwrite: true);

        var projector = new TestProjectorConcurrent(containerClient);
        var context = CreateContext(fixture, new TestEvent { Value = 10 });

        // First update should succeed
        var result1 = await projector.HandleEvent(context);
        await Assert.That(result1).IsEqualTo(EventHandlingStatus.Success);

        // Now simulate concurrent modification: modify the blob directly with a different value
        var modifiedState = new ConcurrentState { Value = 999 };
        var modifiedJson = JsonSerializer.SerializeToUtf8Bytes(modifiedState);
        await blobClient.UploadAsync(new MemoryStream(modifiedJson), overwrite: true);

        // This should now fail with 412 because the ETag won't match
        var result2 = await projector.HandleEvent(context);
        await Assert.That(result2).IsEqualTo(EventHandlingStatus.Ignored);
    }

    // Note: GetBlobName is protected, so we can't test it directly
    // But we can verify it works by checking the blob names used in other tests

    static IMessageConsumeContext CreateContext(IntegrationFixture fixture, object message) =>
        new MessageConsumeContext(
            eventId: Guid.NewGuid().ToString(),
            eventType: message.GetType().Name,
            contentType: "application/json",
            stream: "test-stream",
            eventNumber: 0,
            streamPosition: 0,
            globalPosition: 0,
            sequence: 0,
            created: DateTime.UtcNow,
            message: message,
            metadata: new Metadata(),
            subscriptionId: "test-subscription",
            cancellationToken: CancellationToken.None
        );

    // Test state classes
    class SyncState {
        public int Value { get; set; }
        public int Counter { get; set; }
    }

    class SyncContextState {
        public int Value { get; set; }
        public string StreamId { get; set; } = "";
    }

    class AsyncState {
        public int Value { get; set; }
    }

    class AsyncContextState {
        public int Value { get; set; }
        public string EventName { get; set; } = "";
    }

    class ConcurrentState {
        public int Value { get; set; }
    }

    class NoHandlerState { }

    // Test projector classes - one for each On method variant
    class TestProjectorWithSyncStateHandler : StorageBlobsProjector<SyncState> {
        public TestProjectorWithSyncStateHandler(BlobContainerClient container) : base(container) {
            On<TestEvent>((ctx, state) => {
                state.Value += ((TestEvent)ctx.Message).Value;
                state.Counter++;
                return state;
            });
        }
    }

    class TestProjectorWithSyncContextStateHandler : StorageBlobsProjector<SyncContextState> {
        public TestProjectorWithSyncContextStateHandler(BlobContainerClient container) : base(container) {
            On<TestEvent>((ctx, state) => {
                state.Value += ((TestEvent)ctx.Message).Value;
                state.StreamId = ctx.Stream.GetId();
                return state;
            });
        }
    }

    class TestProjectorWithAsyncStateHandler : StorageBlobsProjector<AsyncState> {
        public TestProjectorWithAsyncStateHandler(BlobContainerClient container) : base(container) {
            On<TestEvent>(async (ctx, state) => {
                await Task.Delay(1);
                state.Value += ((TestEvent)ctx.Message).Value;
                return state;
            });
        }
    }

    class TestProjectorWithAsyncContextStateHandler : StorageBlobsProjector<AsyncContextState> {
        public TestProjectorWithAsyncContextStateHandler(BlobContainerClient container) : base(container) {
            On<TestEvent>(async (ctx, state) => {
                await Task.Delay(1);
                state.Value += ((TestEvent)ctx.Message).Value;
                state.EventName = ((TestEvent)ctx.Message).Name;
                return state;
            });
        }
    }

    class TestProjectorNoHandler : StorageBlobsProjector<NoHandlerState> {
        public TestProjectorNoHandler(BlobContainerClient container) : base(container) {
            // No handlers registered
        }
    }

    class TestProjectorConcurrent : StorageBlobsProjector<ConcurrentState> {
        public TestProjectorConcurrent(BlobContainerClient container) : base(container) {
            On<TestEvent>((ctx, state) => {
                state.Value += ((TestEvent)ctx.Message).Value;
                return state;
            });
        }
    }
}
