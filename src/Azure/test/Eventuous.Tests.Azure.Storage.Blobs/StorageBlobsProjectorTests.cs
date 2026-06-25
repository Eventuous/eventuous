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
    /// Creates a test container for the given scenario, surfacing the handler type and test case.
    /// Returns the container name for use with the new constructor.
    /// </summary>
    async Task<string> SetupContainer(string scenarioName) {
        var containerName = $"test-{scenarioName}";
        var client = fixture.BlobServiceClient.GetBlobContainerClient(containerName);
        await client.CreateAsync();
        return containerName;
    }

    /// <summary>
    /// Gets a BlobContainerClient for the given container name
    /// </summary>
    BlobContainerClient GetContainer(string containerName) =>
        fixture.BlobServiceClient.GetBlobContainerClient(containerName);

    /// <summary>
    /// Sets up initial blob state for update scenarios
    /// </summary>
    async Task SetupExistingBlob<TState>(string containerName, string blobName, TState initialState) {
        var blobClient = GetContainer(containerName).GetBlobClient(blobName);
        var json = JsonSerializer.SerializeToUtf8Bytes(initialState);
        await blobClient.UploadAsync(new MemoryStream(json), overwrite: true);
    }

    /// <summary>
    /// Gets the state from blob, surfacing the expected state type
    /// </summary>
    async Task<TState> GetBlobState<TState>(string containerName, string blobName) {
        var blobClient = GetContainer(containerName).GetBlobClient(blobName);
        var blob = await blobClient.DownloadContentAsync();
        return blob.Value.Content.ToObjectFromJson<TState>(JsonSerializerOptions.Web)!;
    }

    /// <summary>
    /// Asserts that the projector result is Success
    /// </summary>
    static async Task AssertSuccess(EventHandlingStatus result) => await Assert.That(result).IsEqualTo(EventHandlingStatus.Success);

    /// <summary>
    /// Asserts that the projector result is Ignored
    /// </summary>
    static async Task AssertIgnored(EventHandlingStatus result) => await Assert.That(result).IsEqualTo(EventHandlingStatus.Ignored);

    /// <summary>
    /// Asserts that the projector result is Failure
    /// </summary>
    static async Task AssertFailure(EventHandlingStatus result) => await Assert.That(result).IsEqualTo(EventHandlingStatus.Failure);

    // ========== SYNC STATE HANDLER TESTS ==========

    [Test]
    public async Task SyncStateHandler_NewBlob_ShouldCreateAndStoreState() {
        // Arrange
        var containerName = await SetupContainer("sync-state-new");
        var projector = new SyncStateProjector(fixture.BlobServiceClient, containerName);
        var context = CreateContext(new TestEvent { Value = 10 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<SyncState>(containerName, $"{DefaultStream}/SyncState.json");
        await Assert.That(state.Value).IsEqualTo(10);
    }

    [Test]
    public async Task SyncStateHandler_ExistingBlob_ShouldUpdateState() {
        // Arrange
        var containerName = await SetupContainer("sync-state-existing");
        var blobName = $"{DefaultStream}/SyncState.json";

        await SetupExistingBlob(containerName, blobName, new SyncState { Value = 5 });

        var projector = new SyncStateProjector(fixture.BlobServiceClient, containerName);
        var context = CreateContext(new TestEvent { Value = 10 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state.Value).IsEqualTo(15); // 5 + 10
        await Assert.That(state.Counter).IsEqualTo(1);
    }

    // ========== SYNC CONTEXT-AWARE HANDLER TESTS ==========

    [Test]
    public async Task SyncContextAwareHandler_NewBlob_ShouldUseContextAndStoreState() {
        // Arrange
        var containerName = await SetupContainer("sync-context-new");
        var projector = new SyncContextAwareProjector(fixture.BlobServiceClient, containerName);
        var context = CreateContext(new TestEvent { Value = 20 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<SyncContextState>(containerName, $"{DefaultStream}/SyncContextState.json");
        await Assert.That(state.Value).IsEqualTo(20);
        await Assert.That(state.StreamId).IsEqualTo(DefaultStream);
    }

    // ========== ASYNC STATE HANDLER TESTS ==========

    [Test]
    public async Task AsyncStateHandler_NewBlob_ShouldCreateAndStoreState() {
        // Arrange
        var containerName = await SetupContainer("async-state-new");
        var projector = new AsyncStateProjector(fixture.BlobServiceClient, containerName);
        var context = CreateContext(new TestEvent { Value = 30 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<AsyncState>(containerName, $"{DefaultStream}/AsyncState.json");
        await Assert.That(state.Value).IsEqualTo(30);
    }

    [Test]
    public async Task AsyncStateHandler_ExistingBlob_ShouldUpdateState() {
        // Arrange
        var containerName = await SetupContainer("async-state-existing");
        var blobName = $"{DefaultStream}/AsyncState.json";

        await SetupExistingBlob(containerName, blobName, new AsyncState { Value = 5 });

        var projector = new AsyncStateProjector(fixture.BlobServiceClient, containerName);
        var context = CreateContext(new TestEvent { Value = 35 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<AsyncState>(containerName, blobName);
        await Assert.That(state.Value).IsEqualTo(40); // 5 + 35
    }

    // ========== ASYNC CONTEXT-AWARE HANDLER TESTS ==========

    [Test]
    public async Task AsyncContextAwareHandler_NewBlob_ShouldUseContextAndStoreState() {
        // Arrange
        var containerName = await SetupContainer("async-context-new");
        var projector = new AsyncContextAwareProjector(fixture.BlobServiceClient, containerName);
        var context = CreateContext(new TestEvent { Value = 40, Name = "AsyncContext" });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<AsyncContextState>(containerName, $"{DefaultStream}/AsyncContextState.json");
        await Assert.That(state.Value).IsEqualTo(40);
        await Assert.That(state.EventName).IsEqualTo("AsyncContext");
    }

    [Test]
    public async Task AsyncContextAwareHandler_ExistingBlob_ShouldUpdateStateAndContext() {
        // Arrange
        var containerName = await SetupContainer("async-context-existing");
        var blobName = $"{DefaultStream}/AsyncContextState.json";

        await SetupExistingBlob(containerName, blobName, new AsyncContextState { Value = 10, EventName = "Initial" });

        var projector = new AsyncContextAwareProjector(fixture.BlobServiceClient, containerName);
        var context = CreateContext(new TestEvent { Value = 50, Name = "Update" });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<AsyncContextState>(containerName, blobName);
        await Assert.That(state.Value).IsEqualTo(60); // 10 + 50
        await Assert.That(state.EventName).IsEqualTo("Update");
    }

    // ========== EDGE CASE TESTS ==========

    [Test]
    public async Task NoHandler_ShouldReturnIgnored() {
        // Arrange
        var containerName = await SetupContainer("no-handler");
        var projector = new NoHandlerProjector(fixture.BlobServiceClient, containerName);
        var context = CreateContext(new TestEvent { Value = 100 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertIgnored(result);
    }

    // ========== CUSTOM BLOB ID TESTS ==========

    [Test]
    public async Task CustomBlobId_NewBlob_ShouldUseEventIdForBlobName() {
        // Arrange
        var containerName = await SetupContainer("custom-blobid-new");
        var eventId = Guid.NewGuid().ToString();
        var projector = new CustomBlobIdProjector(fixture.BlobServiceClient, containerName);
        var context = CreateContext(new TestEvent { Id = eventId, Value = 100 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var blobName = $"{eventId}/CustomBlobIdState.json";
        var state = await GetBlobState<CustomBlobIdState>(containerName, blobName);
        await Assert.That(state.Value).IsEqualTo(100);
    }

    [Test]
    public async Task CustomBlobId_ExistingBlob_ShouldUpdateWithEventId() {
        // Arrange
        var containerName = await SetupContainer("custom-blobid-existing");
        var eventId = Guid.NewGuid().ToString();
        var blobName = $"{eventId}/CustomBlobIdState.json";

        await SetupExistingBlob(containerName, blobName, new CustomBlobIdState { Value = 5 });

        var projector = new CustomBlobIdProjector(fixture.BlobServiceClient, containerName);
        var context = CreateContext(new TestEvent { Id = eventId, Value = 100 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var state = await GetBlobState<CustomBlobIdState>(containerName, blobName);
        await Assert.That(state.Value).IsEqualTo(105); // 5 + 100
    }

    // ========== RACE RETRY TESTS ==========

    [Test]
    public async Task RaceRetries_WithOneRetry_ShouldSucceedAfterRaceCondition() {
        // Arrange
        var containerName = await SetupContainer("race-retry");
        var blobName = $"{DefaultStream}/ConcurrentState.json";

        await SetupExistingBlob(containerName, blobName, new ConcurrentState { Value = 1 });

        var projector = new RaceRetryProjector(fixture.BlobServiceClient, containerName,
            messWithState: () => {
                // Simulate concurrent modification: modify the blob directly with a different value
                var modifiedState = new ConcurrentState { Value = 999 };
                var modifiedJson = JsonSerializer.SerializeToUtf8Bytes(modifiedState);
                var blobClient = GetContainer(containerName).GetBlobClient(blobName);
                blobClient.Upload(new MemoryStream(modifiedJson), overwrite: true);
            });
        var context = CreateContext(new TestEvent { Value = 10 });

        // Act
        var result = await projector.HandleEvent(context);

        // Assert - with retry, this should succeed
        await AssertSuccess(result);

        var state = await GetBlobState<ConcurrentState>(containerName, blobName);
        // First attempt: concurrent modification sets value to 999, causing 412
        // Retry: reads 999, adds 10, succeeds
        await Assert.That(state.Value).IsEqualTo(1009); // 999 + 10 (retry succeeded)
    }

    [Test]
    public async Task ConcurrentAdditionOfNewBlob_ShouldReturnFailure() {
        // Arrange
        var containerName = await SetupContainer("concurrent-new");
        var blobName = "stream/ConcurrentState.json";

        var projector = new ConcurrentModificationProjector(fixture.BlobServiceClient, containerName,
        messWithState: () => {
            // Simulate concurrent modification: modify the blob directly with a different value
            var modifiedState = new ConcurrentState { Value = 999 };
            var modifiedJson = JsonSerializer.SerializeToUtf8Bytes(modifiedState);
            var blobClient = GetContainer(containerName).GetBlobClient(blobName);
            blobClient.Upload(new MemoryStream(modifiedJson), overwrite: true);
        }, onCall: 1);
        var context = CreateContext(new TestEvent { Value = 10 });

        // This should now fail with 412 because the ETag won't match
        var result2 = await projector.HandleEvent(context);
        await StorageBlobsProjectorTests.AssertFailure(result2);
    }

    [Test]
    public async Task ConcurrentModificationOfExistingBlob_ShouldReturnFailure() {
        // Arrange
        var containerName = await SetupContainer("concurrent-existing");
        var blobName = "stream/ConcurrentState.json";

        await SetupExistingBlob(containerName, blobName, new ConcurrentState { Value = 1 });

        var projector = new ConcurrentModificationProjector(fixture.BlobServiceClient, containerName,
        messWithState: () => {
            // Simulate concurrent modification: modify the blob directly with a different value
            var modifiedState = new ConcurrentState { Value = 999 };
            var modifiedJson = JsonSerializer.SerializeToUtf8Bytes(modifiedState);
            var blobClient = GetContainer(containerName).GetBlobClient(blobName);
            blobClient.Upload(new MemoryStream(modifiedJson), overwrite: true);
        }, onCall: 2);
        var context = CreateContext(new TestEvent { Value = 10 });

        // First update should succeed
        var result1 = await projector.HandleEvent(context);
        await AssertSuccess(result1);

        // This should now fail with 412 because the ETag won't match
        var result2 = await projector.HandleEvent(context);
        await StorageBlobsProjectorTests.AssertFailure(result2);
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

    class CustomBlobIdState {
        public int Value { get; set; }
    }

    // ========== TEST PROJECTOR CLASSES
    // Intent: Each class name explicitly surfaces the handler pattern being tested ==========

    /// <summary>
    /// Tests sync handler: On<TEvent>(Func<Context, State, State>)
    /// </summary>
    class SyncStateProjector : StorageBlobsProjector<SyncState> {
        public SyncStateProjector(BlobServiceClient serviceClient, string containerName)
            : base(serviceClient, containerName) {
            On<TestEvent>((ctx, state) => {
                state.Value += ctx.Message.Value;
                state.Counter++;
                return state;
            });
        }
    }

    /// <summary>
    /// Tests sync context-aware handler: On<TEvent>(Func<Context, State, State>) with context access
    /// </summary>
    class SyncContextAwareProjector : StorageBlobsProjector<SyncContextState> {
        public SyncContextAwareProjector(BlobServiceClient serviceClient, string containerName)
            : base(serviceClient, containerName) {
            On<TestEvent>((ctx, state) => {
                state.Value += ctx.Message.Value;
                state.StreamId = ctx.Stream.GetId();
                return state;
            });
        }
    }

    /// <summary>
    /// Tests async handler: On<TEvent>(Func<Context, State, ValueTask<State>>)
    /// </summary>
    class AsyncStateProjector : StorageBlobsProjector<AsyncState> {
        public AsyncStateProjector(BlobServiceClient serviceClient, string containerName)
            : base(serviceClient, containerName) {
            On<TestEvent>(async (ctx, state) => {
                await Task.Delay(1);
                state.Value += ctx.Message.Value;
                return state;
            });
        }
    }

    /// <summary>
    /// Tests async context-aware handler: On<TEvent>(Func<Context, State, ValueTask<State>>) with context access
    /// </summary>
    class AsyncContextAwareProjector : StorageBlobsProjector<AsyncContextState> {
        public AsyncContextAwareProjector(BlobServiceClient serviceClient, string containerName)
           : base(serviceClient, containerName) {
            On<TestEvent>(async (ctx, state) => {
                await Task.Delay(1);
                state.Value += ctx.Message.Value;
                state.EventName = ctx.Message.Name;
                return state;
            });
        }
    }

    /// <summary>
    /// Tests scenario with no handlers registered
    /// </summary>
    class NoHandlerProjector : StorageBlobsProjector<NoHandlerState> {
        public NoHandlerProjector(BlobServiceClient serviceClient, string containerName)
            : base(serviceClient, containerName) { }
    }

    /// <summary>
    /// Tests concurrent modification scenario (ETag mismatch)
    /// </summary>
    class ConcurrentModificationProjector : StorageBlobsProjector<ConcurrentState> {
        private int _callCount = 0;
        public ConcurrentModificationProjector(BlobServiceClient serviceClient, string containerName, Action messWithState, int onCall)
             : base(serviceClient, containerName) {
            On<TestEvent>((ctx, state) => {
                if (++_callCount == onCall)
                    messWithState();
                state.Value += ctx.Message.Value;
                return state;
            });
        }
    }

    /// <summary>
    /// Tests custom blob ID using getBlobId parameter
    /// </summary>
    class CustomBlobIdProjector : StorageBlobsProjector<CustomBlobIdState> {
        public CustomBlobIdProjector(BlobServiceClient serviceClient, string containerName)
            : base(serviceClient, containerName) {
            On<TestEvent>(async (ctx, state) => {
                state.Value += ctx.Message.Value;
                return state;
            }, getBlobId: ctx => new ValueTask<string>(ctx.Message.Id));
        }
    }

    /// <summary>
    /// Tests race condition retry with RaceRetries = 1
    /// </summary>
    class RaceRetryProjector : StorageBlobsProjector<ConcurrentState> {
        private int _callCount = 0;
        private readonly Action _messWithState;

        public RaceRetryProjector(
            BlobServiceClient serviceClient, 
            string containerName,
            Action messWithState
        ) : base(serviceClient, containerName, projectorOptions: new StorageBlobProjectorOptions<ConcurrentState> { RaceRetries = 1 }) {
            _messWithState = messWithState;

            On<TestEvent>((ctx, state) => {
                if (++_callCount == 1)
                    _messWithState();
                state.Value += ctx.Message.Value;
                return state;
            });
        }
    }
}
