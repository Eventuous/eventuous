using System.Text.Json;
using Azure.Storage.Blobs;
using Eventuous.Azure.Storage.Blobs;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Tests.Azure.Storage.Blobs.Fixtures;

namespace Eventuous.Tests.Azure.Storage.Blobs;

[ClassDataSource<IntegrationFixture>]
public class StorageBlobsProjectorTests(IntegrationFixture fixture) {
    const string DefaultStream = "stream";

    // ========== HELPER METHODS (surface intent through naming) ==========

    /// <summary>
    /// Creates a test container for the given scenario, surfacing the handler type and test case
    /// </summary>
    async Task<BlobContainerClient> SetupContainer(string scenarioName) {
        var containerName = $"test-{scenarioName}";
        var client = fixture.BlobServiceClient.GetBlobContainerClient(containerName);
        await client.CreateAsync();
        return client;
    }

    /// <summary>
    /// Sets up initial blob state for update scenarios
    /// </summary>
    async Task SetupExistingBlob<TState>(BlobContainerClient container, string blobName, TState initialState) {
        var blobClient = container.GetBlobClient(blobName);
        var json = JsonSerializer.SerializeToUtf8Bytes(initialState);
        await blobClient.UploadAsync(new MemoryStream(json), overwrite: true);
    }

    /// <summary>
    /// Gets the state from blob, surfacing the expected state type
    /// </summary>
    async Task<TState> GetBlobState<TState>(BlobContainerClient container, string blobName) {
        var blobClient = container.GetBlobClient(blobName);
        var blob = await blobClient.DownloadContentAsync();
        return blob.Value.Content.ToObjectFromJson<TState>(JsonSerializerOptions.Web)!;
    }

    /// <summary>
    /// Asserts that the projector result is Success
    /// </summary>
    async Task AssertSuccess(EventHandlingStatus result) {
        await Assert.That(result).IsEqualTo(EventHandlingStatus.Success);
    }

    /// <summary>
    /// Asserts that the projector result is Ignored
    /// </summary>
    async Task AssertIgnored(EventHandlingStatus result) {
        await Assert.That(result).IsEqualTo(EventHandlingStatus.Ignored);
    }

    // ========== SYNC STATE HANDLER TESTS ==========

    [Test]
    public async Task SyncStateHandler_NewBlob_ShouldCreateAndStoreState() {
        // Arrange
        var container = await SetupContainer("sync-state-new");
        var projector = new SyncStateProjector(container);
        var context = CreateContext(new TestEvent { Value = 10 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<SyncState>(container, $"{DefaultStream}/SyncState.json");
        await Assert.That(state.Value).IsEqualTo(10);
    }

    [Test]
    public async Task SyncStateHandler_ExistingBlob_ShouldUpdateState() {
        // Arrange
        var container = await SetupContainer("sync-state-existing");
        var blobName = $"{DefaultStream}/SyncState.json";

        await SetupExistingBlob(container, blobName, new SyncState { Value = 5 });

        var projector = new SyncStateProjector(container);
        var context = CreateContext(new TestEvent { Value = 10 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<SyncState>(container, blobName);
        await Assert.That(state.Value).IsEqualTo(15); // 5 + 10
        await Assert.That(state.Counter).IsEqualTo(1);
    }

    // ========== SYNC CONTEXT-AWARE HANDLER TESTS ==========

    [Test]
    public async Task SyncContextAwareHandler_NewBlob_ShouldUseContextAndStoreState() {
        // Arrange
        var container = await SetupContainer("sync-context-new");
        var projector = new SyncContextAwareProjector(container);
        var context = CreateContext(new TestEvent { Value = 20 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<SyncContextState>(container, $"{DefaultStream}/SyncContextState.json");
        await Assert.That(state.Value).IsEqualTo(20);
        await Assert.That(state.StreamId).IsEqualTo(DefaultStream);
    }

    // ========== ASYNC STATE HANDLER TESTS ==========

    [Test]
    public async Task AsyncStateHandler_NewBlob_ShouldCreateAndStoreState() {
        // Arrange
        var container = await SetupContainer("async-state-new");
        var projector = new AsyncStateProjector(container);
        var context = CreateContext(new TestEvent { Value = 30 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<AsyncState>(container, $"{DefaultStream}/AsyncState.json");
        await Assert.That(state.Value).IsEqualTo(30);
    }

    [Test]
    public async Task AsyncStateHandler_ExistingBlob_ShouldUpdateState() {
        // Arrange
        var container = await SetupContainer("async-state-existing");
        var blobName = $"{DefaultStream}/AsyncState.json";

        await SetupExistingBlob(container, blobName, new AsyncState { Value = 5 });

        var projector = new AsyncStateProjector(container);
        var context = CreateContext(new TestEvent { Value = 35 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<AsyncState>(container, blobName);
        await Assert.That(state.Value).IsEqualTo(40); // 5 + 35
    }

    // ========== ASYNC CONTEXT-AWARE HANDLER TESTS ==========

    [Test]
    public async Task AsyncContextAwareHandler_NewBlob_ShouldUseContextAndStoreState() {
        // Arrange
        var container = await SetupContainer("async-context-new");
        var projector = new AsyncContextAwareProjector(container);
        var context = CreateContext(new TestEvent { Value = 40, Name = "AsyncContext" });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<AsyncContextState>(container, $"{DefaultStream}/AsyncContextState.json");
        await Assert.That(state.Value).IsEqualTo(40);
        await Assert.That(state.EventName).IsEqualTo("AsyncContext");
    }

    [Test]
    public async Task AsyncContextAwareHandler_ExistingBlob_ShouldUpdateStateAndContext() {
        // Arrange
        var container = await SetupContainer("async-context-existing");
        var blobName = $"{DefaultStream}/AsyncContextState.json";

        await SetupExistingBlob(container, blobName, new AsyncContextState { Value = 10, EventName = "Initial" });

        var projector = new AsyncContextAwareProjector(container);
        var context = CreateContext(new TestEvent { Value = 50, Name = "Update" });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<AsyncContextState>(container, blobName);
        await Assert.That(state.Value).IsEqualTo(60); // 10 + 50
        await Assert.That(state.EventName).IsEqualTo("Update");
    }

    // ========== EDGE CASE TESTS ==========

    [Test]
    public async Task NoHandler_ShouldReturnIgnored() {
        // Arrange
        var container = await SetupContainer("no-handler");
        var projector = new NoHandlerProjector(container);
        var context = CreateContext(new TestEvent { Value = 100 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertIgnored(result);
    }

    [Test]
    public async Task ConcurrentModification_ShouldReturnIgnored() {
        // Arrange
        var container = await SetupContainer("concurrent");
        var blobName = "concurrent-stream/ConcurrentState.json";

        await SetupExistingBlob(container, blobName, new ConcurrentState { Value = 1 });

        var projector = new ConcurrentModificationProjector(container);
        var context = CreateContext(new TestEvent { Value = 10 });

        // First update should succeed
        var result1 = await projector.HandleEvent(context);
        await AssertSuccess(result1);

        // Simulate concurrent modification: modify the blob directly with a different value
        var modifiedState = new ConcurrentState { Value = 999 };
        var modifiedJson = JsonSerializer.SerializeToUtf8Bytes(modifiedState);
        var blobClient = container.GetBlobClient(blobName);
        await blobClient.UploadAsync(new MemoryStream(modifiedJson), overwrite: true);

        // This should now fail with 412 because the ETag won't match
        var result2 = await projector.HandleEvent(context);
        await AssertIgnored(result2);
    }

    // ========== TEST CONTEXT FACTORY ==========

    static IMessageConsumeContext CreateContext(object message) =>
        new MessageConsumeContext(
            eventId: Guid.NewGuid().ToString(),
            eventType: message.GetType().Name,
            contentType: "application/json",
            stream: DefaultStream,
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

    // ========== TEST STATE CLASSES ==========

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

    // ========== TEST PROJECTOR CLASSES
    // Intent: Each class name explicitly surfaces the handler pattern being tested ==========

    /// <summary>
    /// Tests sync handler: On<TEvent>(Func<Context, State, State>)
    /// </summary>
    class SyncStateProjector : StorageBlobsProjector<SyncState> {
        public SyncStateProjector(BlobContainerClient container) : base(container) {
            On<TestEvent>((ctx, state) => {
                state.Value += ((TestEvent)ctx.Message).Value;
                state.Counter++;
                return state;
            });
        }
    }

    /// <summary>
    /// Tests sync context-aware handler: On<TEvent>(Func<Context, State, State>) with context access
    /// </summary>
    class SyncContextAwareProjector : StorageBlobsProjector<SyncContextState> {
        public SyncContextAwareProjector(BlobContainerClient container) : base(container) {
            On<TestEvent>((ctx, state) => {
                state.Value += ((TestEvent)ctx.Message).Value;
                state.StreamId = ctx.Stream.GetId();
                return state;
            });
        }
    }

    /// <summary>
    /// Tests async handler: On<TEvent>(Func<Context, State, ValueTask<State>>)
    /// </summary>
    class AsyncStateProjector : StorageBlobsProjector<AsyncState> {
        public AsyncStateProjector(BlobContainerClient container) : base(container) {
            On<TestEvent>(async (ctx, state) => {
                await Task.Delay(1);
                state.Value += ((TestEvent)ctx.Message).Value;
                return state;
            });
        }
    }

    /// <summary>
    /// Tests async context-aware handler: On<TEvent>(Func<Context, State, ValueTask<State>>) with context access
    /// </summary>
    class AsyncContextAwareProjector : StorageBlobsProjector<AsyncContextState> {
        public AsyncContextAwareProjector(BlobContainerClient container) : base(container) {
            On<TestEvent>(async (ctx, state) => {
                await Task.Delay(1);
                state.Value += ((TestEvent)ctx.Message).Value;
                state.EventName = ((TestEvent)ctx.Message).Name;
                return state;
            });
        }
    }

    /// <summary>
    /// Tests scenario with no handlers registered
    /// </summary>
    class NoHandlerProjector : StorageBlobsProjector<NoHandlerState> {
        public NoHandlerProjector(BlobContainerClient container) : base(container) {
            // No handlers registered - all events should be ignored
        }
    }

    /// <summary>
    /// Tests concurrent modification scenario (ETag mismatch)
    /// </summary>
    class ConcurrentModificationProjector : StorageBlobsProjector<ConcurrentState> {
        public ConcurrentModificationProjector(BlobContainerClient container) : base(container) {
            On<TestEvent>((ctx, state) => {
                state.Value += ((TestEvent)ctx.Message).Value;
                return state;
            });
        }
    }
}
