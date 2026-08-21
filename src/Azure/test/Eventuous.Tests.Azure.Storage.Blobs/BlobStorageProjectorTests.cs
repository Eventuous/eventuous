using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Eventuous.Azure.Storage.Blobs;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Tests.Azure.Storage.Blobs.Fixtures;

namespace Eventuous.Tests.Azure.Storage.Blobs;

[ClassDataSource<IntegrationFixture>]
public class BlobStorageProjectorTests(IntegrationFixture fixture) {
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
        using var stream = new MemoryStream(json);
        await blobClient.UploadAsync(stream, overwrite: true);
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

    /// <summary>
    /// Creates a concurrent-modification action that overwrites the blob directly with a different value
    /// </summary>
    Func<Task> OverwriteBlob(string containerName, string blobName) => async () => {
        var modifiedState = new ConcurrentState { Value = 999 };
        var modifiedJson  = JsonSerializer.SerializeToUtf8Bytes(modifiedState);
        var blobClient    = GetContainer(containerName).GetBlobClient(blobName);
        using var stream  = new MemoryStream(modifiedJson);
        await blobClient.UploadAsync(stream, overwrite: true);
    };

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

    [Test]
    public async Task UnicodeStreamName_ShouldStoreStateWithEncodedMetadata() {
        // Arrange
        var containerName = await SetupContainer("unicode-stream");
        const string streamName = "Booking-Ålesund";
        var projector = new SyncStateProjector(fixture.BlobServiceClient, containerName);
        var context = CreateContext(new TestEvent { Value = 10 }, stream: streamName);

        // Act
        var result = await projector.HandleEvent(context);

        // Assert
        await AssertSuccess(result);

        var blobName = "Ålesund/SyncState.json";
        var state = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state.Value).IsEqualTo(10);

        // Metadata values must be ASCII, so the stream name is stored percent-encoded
        var properties = await GetContainer(containerName).GetBlobClient(blobName).GetPropertiesAsync();
        await Assert.That(properties.Value.Metadata["Stream"]).IsEqualTo(Uri.EscapeDataString(streamName));
    }

    [Test]
    public async Task HandlerThrowingRequestFailed_ShouldPropagateWithoutRaceRetries() {
        // Arrange
        var containerName = await SetupContainer("handler-exception");
        var projector = new ThrowingHandlerProjector(fixture.BlobServiceClient, containerName, raceRetries: 2);
        var context = CreateContext(new TestEvent { Value = 10 });

        // Act & Assert - the handler's own Azure exception propagates instead of being
        // classified as an optimistic concurrency race and retried
        await Assert.ThrowsAsync<RequestFailedException>(() => projector.HandleEvent(context).AsTask());
        await Assert.That(projector.HandlerCalls).IsEqualTo(1);
    }

    // ========== RACE RETRY TESTS ==========

    [Test]
    public async Task RaceRetries_WithOneRetry_ShouldSucceedAfterRaceCondition() {
        // Arrange
        var containerName = await SetupContainer("race-retry");
        var blobName = $"{DefaultStream}/ConcurrentState.json";

        await SetupExistingBlob(containerName, blobName, new ConcurrentState { Value = 1 });

        var projector = new ConcurrentModificationProjector(
            fixture.BlobServiceClient,
            containerName,
            messWithState: OverwriteBlob(containerName, blobName),
            raceRetries: 1);
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
        var blobName = $"{DefaultStream}/ConcurrentState.json";

        var projector = new ConcurrentModificationProjector(
            fixture.BlobServiceClient,
            containerName,
            messWithState: OverwriteBlob(containerName, blobName),
            onCall: 1);
        var context = CreateContext(new TestEvent { Value = 10 });

        // This should now fail with 412 because the ETag won't match
        var result2 = await projector.HandleEvent(context);
        await AssertFailure(result2);
    }

    [Test]
    public async Task ConcurrentModificationOfExistingBlob_ShouldReturnFailure() {
        // Arrange
        var containerName = await SetupContainer("concurrent-existing");
        var blobName = $"{DefaultStream}/ConcurrentState.json";

        await SetupExistingBlob(containerName, blobName, new ConcurrentState { Value = 1 });

        var projector = new ConcurrentModificationProjector(
            fixture.BlobServiceClient,
            containerName,
            messWithState: OverwriteBlob(containerName, blobName),
            onCall: 2);
        var context = CreateContext(new TestEvent { Value = 10 });

        // First update should succeed
        var result1 = await projector.HandleEvent(context);
        await AssertSuccess(result1);

        // This should now fail with 412 because the ETag won't match
        var result2 = await projector.HandleEvent(context);
        await AssertFailure(result2);
    }

    // ========== IDEMPOTENCY TESTS ==========

    [Test]
    public async Task Idempotency_ByMessageId_ShouldIgnoreDuplicateMessage() {
        // Arrange
        var containerName = await SetupContainer("idempotency-messageid");
        var blobName = $"{DefaultStream}/SyncState.json";

        var projector = new IdempotencyProjector(fixture.BlobServiceClient, containerName, IdempotencyMode.ByMessageId);
        var messageId = Guid.NewGuid().ToString();

        // First context with specific message ID
        var context1 = CreateContext(new TestEvent { Value = 10 }, messageId: messageId);

        // Act - first processing should succeed
        var result1 = await projector.HandleEvent(context1);
        await AssertSuccess(result1);

        var state1 = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state1.Value).IsEqualTo(10);

        // Second context with SAME message ID (duplicate)
        var context2 = CreateContext(new TestEvent { Value = 20 }, messageId: messageId);

        // Act - second processing should be ignored
        var result2 = await projector.HandleEvent(context2);
        await AssertIgnored(result2);

        // State should NOT have been updated (still 10, not 30)
        var state2 = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state2.Value).IsEqualTo(10);
    }

    [Test]
    public async Task Idempotency_ByMessageId_ShouldProcessDifferentMessageId() {
        // Arrange
        var containerName = await SetupContainer("idempotency-messageid-different");
        var blobName = $"{DefaultStream}/SyncState.json";

        var projector = new IdempotencyProjector(fixture.BlobServiceClient, containerName, IdempotencyMode.ByMessageId);

        var messageId1 = Guid.NewGuid().ToString();
        var context1 = CreateContext(new TestEvent { Value = 10 }, messageId: messageId1);

        // Act - first message
        var result1 = await projector.HandleEvent(context1);
        await AssertSuccess(result1);

        var state1 = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state1.Value).IsEqualTo(10);

        // Different message ID
        var messageId2 = Guid.NewGuid().ToString();
        var context2 = CreateContext(new TestEvent { Value = 20 }, messageId: messageId2);

        // Act - different message should be processed
        var result2 = await projector.HandleEvent(context2);
        await AssertSuccess(result2);

        // State should have been updated (10 + 20 = 30)
        var state2 = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state2.Value).IsEqualTo(30);
    }

    [Test]
    [Arguments(100u)]
    [Arguments(99u)]
    public async Task Idempotency_ByGlobalPosition_ShouldIgnoreDuplicatePosition(ulong duplicatePosition) {
        // Arrange
        var containerName = await SetupContainer("idempotency-globalposition");
        var blobName = $"{DefaultStream}/SyncState.json";

        var projector = new IdempotencyProjector(fixture.BlobServiceClient, containerName, IdempotencyMode.ByGlobalPosition);

        // First context with specific global position
        var context1 = CreateContext(new TestEvent { Value = 10 }, globalPosition: 100u);

        // Act - first processing should succeed
        var result1 = await projector.HandleEvent(context1);
        await AssertSuccess(result1);

        var state1 = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state1.Value).IsEqualTo(10);

        // Second context with SAME global position (duplicate)
        var context2 = CreateContext(new TestEvent { Value = 20 }, globalPosition: duplicatePosition);

        // Act - second processing should be ignored
        var result2 = await projector.HandleEvent(context2);
        await AssertIgnored(result2);

        // State should NOT have been updated (still 10, not 30)
        var state2 = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state2.Value).IsEqualTo(10);
    }

    [Test]
    public async Task Idempotency_ByGlobalPosition_ShouldProcessDifferentPosition() {
        // Arrange
        var containerName = await SetupContainer("idempotency-globalposition-different");
        var blobName = $"{DefaultStream}/SyncState.json";

        var projector = new IdempotencyProjector(fixture.BlobServiceClient, containerName, IdempotencyMode.ByGlobalPosition);

        // First context with specific global position
        var context1 = CreateContext(new TestEvent { Value = 10 }, globalPosition: 100);

        // Act - first processing should succeed
        var result1 = await projector.HandleEvent(context1);
        await AssertSuccess(result1);

        var state1 = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state1.Value).IsEqualTo(10);

        // Different global position
        var context2 = CreateContext(new TestEvent { Value = 20 }, globalPosition: 101u);

        // Act - different position should be processed
        var result2 = await projector.HandleEvent(context2);
        await AssertSuccess(result2);

        // State should have been updated (10 + 20 = 30)
        var state2 = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state2.Value).IsEqualTo(30);
    }

    [Test]
    public async Task Idempotency_None_ShouldAlwaysProcess() {
        // Arrange - explicitly set to None (which is also the default)
        var containerName = await SetupContainer("idempotency-none");
        var blobName = $"{DefaultStream}/SyncState.json";

        var projector = new IdempotencyProjector(fixture.BlobServiceClient, containerName, IdempotencyMode.None);
        var messageId = Guid.NewGuid().ToString();

        // First context
        var context1 = CreateContext(new TestEvent { Value = 10 }, messageId: messageId);

        // Act
        var result1 = await projector.HandleEvent(context1);
        await AssertSuccess(result1);

        var state1 = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state1.Value).IsEqualTo(10);

        // Second context with SAME message ID - should still process
        var context2 = CreateContext(new TestEvent { Value = 20 }, messageId: messageId);

        // Act - should process even with same message ID
        var result2 = await projector.HandleEvent(context2);
        await AssertSuccess(result2);

        // State should have been updated (10 + 20 = 30) - no idempotency
        var state2 = await GetBlobState<SyncState>(containerName, blobName);
        await Assert.That(state2.Value).IsEqualTo(30);
    }

    // ========== TEST CONTEXT FACTORY ==========

    static IMessageConsumeContext CreateContext(object message, string? messageId = null, ulong globalPosition = 0, string stream = DefaultStream) =>
        new MessageConsumeContext(
            eventId: messageId ?? Guid.NewGuid().ToString(),
            eventType: message.GetType().Name,
            contentType: "application/json",
            stream: stream,
            eventNumber: 0,
            streamPosition: 0,
            globalPosition: globalPosition,
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
    class SyncStateProjector : BlobStorageProjector<SyncState> {
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
    class SyncContextAwareProjector : BlobStorageProjector<SyncContextState> {
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
    class AsyncStateProjector : BlobStorageProjector<AsyncState> {
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
    class AsyncContextAwareProjector : BlobStorageProjector<AsyncContextState> {
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
    class NoHandlerProjector : BlobStorageProjector<NoHandlerState> {
        public NoHandlerProjector(BlobServiceClient serviceClient, string containerName)
            : base(serviceClient, containerName) { }
    }

    /// <summary>
    /// Tests concurrent modification scenario with configurable race retries and onCall
    /// </summary>
    class ConcurrentModificationProjector : BlobStorageProjector<ConcurrentState> {
        int _callCount;
        readonly Func<Task>? _messWithState;
        readonly int _onCall;

        public ConcurrentModificationProjector(
            BlobServiceClient serviceClient,
            string containerName,
            Func<Task>? messWithState = null,
            int onCall = 1,
            int raceRetries = 0
        ) : base(serviceClient, containerName, projectorOptions: new BlobStorageProjectorOptions { RaceRetries = raceRetries }) {
            _messWithState = messWithState;
            _onCall = onCall;

            On<TestEvent>(async (ctx, state) => {
                if (_messWithState != null && ++_callCount == _onCall)
                    await _messWithState();
                state.Value += ctx.Message.Value;
                return state;
            });
        }
    }

    /// <summary>
    /// Tests custom blob ID using getBlobId parameter
    /// </summary>
    class CustomBlobIdProjector : BlobStorageProjector<CustomBlobIdState> {
        public CustomBlobIdProjector(BlobServiceClient serviceClient, string containerName)
            : base(serviceClient, containerName) {
            On<TestEvent>(async (ctx, state) => {
                state.Value += ctx.Message.Value;
                return state;
            }, getBlobId: ctx => new ValueTask<string>(ctx.Message.Id));
        }
    }

    /// <summary>
    /// Tests that a handler-thrown RequestFailedException is not mistaken for a blob race
    /// </summary>
    class ThrowingHandlerProjector : BlobStorageProjector<SyncState> {
        public int HandlerCalls { get; private set; }

        public ThrowingHandlerProjector(BlobServiceClient serviceClient, string containerName, int raceRetries)
            : base(serviceClient, containerName, new BlobStorageProjectorOptions { RaceRetries = raceRetries }) {
            Func<IMessageConsumeContext<TestEvent>, SyncState, SyncState> handler = (_, _) => {
                HandlerCalls++;
                throw new RequestFailedException(409, "Handler-side conflict talking to another service");
            };
            On(handler);
        }
    }

    /// <summary>
    /// Tests idempotency with configurable mode
    /// </summary>
    class IdempotencyProjector : BlobStorageProjector<SyncState> {
        public IdempotencyProjector(BlobServiceClient serviceClient, string containerName, IdempotencyMode mode)
            : base(serviceClient, containerName, projectorOptions: new BlobStorageProjectorOptions { IdempotencyMode = mode }) {
            On<TestEvent>((ctx, state) => {
                state.Value += ctx.Message.Value;
                return state;
            });
        }
    }
}
