using Azure;
using Azure.Storage.Blobs.Models;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Microsoft.Extensions.Options;
using System.Text.Json;

using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Azure.Storage.Blobs;

/// <summary>
/// Projects event store events to Azure Blob Storage as state objects of type T.
/// </summary>
/// <remarks>
/// <para>
/// This projector works by maintaining a state object of type T in Azure Blob Storage for each event stream.
/// When an event is received, it retrieves the current state blob (or creates a new state instance if the blob doesn't exist),
/// applies the event to the state using the registered event handler, and uploads the updated state back to Blob Storage.
/// The projector uses optimistic concurrency control via ETags to handle concurrent updates, and provides virtual methods
/// for customizing blob naming conventions. Multiple event types can be handled by registering handlers using the On(TEvent) methods.
/// The optional getBlobId parameter in event registration allows custom blob ID generation, which is useful when the default
/// stream ID from context.Stream.GetId() needs to be overridden, such as using event metadata or custom business logic.
/// </para>
/// </remarks>
public class StorageBlobsProjector<T> : BaseEventHandler where T : class, new() {
    /// <summary>Azure Blob Storage container client.</summary>
    protected readonly BlobContainerClient ContainerClient;

    readonly JsonSerializerOptions _jsonOptions;
    readonly Dictionary<Type, Func<IMessageConsumeContext, ValueTask<EventHandlingStatus>>> _handlers = new();
    readonly ITypeMapper _map;

    /// <summary>Deserialization function for blob content to T.</summary>
    protected readonly Func<BinaryData, T> Deserialize;

    /// <summary>Serialization function for T to byte array.</summary>
    protected readonly Func<T, byte[]> Serialize;
    private readonly int _raceRetries;
    private readonly IdempotencyMode _idempotencyMode;

    /// <summary>Delegate for custom blob ID generation from consume context.</summary>
    /// <typeparam name="TEvent">Event type being consumed.</typeparam>
    /// <param name="context">Event consume context.</param>
    /// <returns>Blob ID as string.</returns>
    public delegate ValueTask<string> GetBlobId<TEvent>(IMessageConsumeContext<TEvent> context) where TEvent : class;

    /// <summary>
    /// Initializes projector with existing container client.
    /// </summary>
    /// <param name="container">Azure Blob Storage container client.</param>
    /// <param name="projectorOptions">Optional projector configuration.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <param name="mapper">Optional type mapper for event type resolution.</param>
    public StorageBlobsProjector(
        BlobContainerClient container,
        StorageBlobProjectorOptions<T>? projectorOptions = null,
        IOptions<JsonSerializerOptions>? options = null,
        ITypeMapper? mapper = null
    ) {
        ContainerClient = container;
        _jsonOptions = projectorOptions?.JsonOptions ?? options?.Value ?? new(JsonSerializerOptions.Web);
        _map = mapper ?? TypeMap.Instance;
        Deserialize = projectorOptions?.Deserialize ?? ToObjectFromJson;
        Serialize = projectorOptions?.Serialize ?? SerializeToUtf8Bytes;
        _raceRetries = projectorOptions?.RaceRetries ?? 0;
        _idempotencyMode = projectorOptions?.IdempotencyMode ?? IdempotencyMode.None;
    }

    /// <summary>
    /// Initializes projector with service client and container name.
    /// </summary>
    /// <param name="serviceClient">Azure Blob Storage service client.</param>
    /// <param name="containerName">Name of the container to use.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <param name="mapper">Optional type mapper for event type resolution.</param>
    /// <param name="projectorOptions">Optional projector configuration.</param>
    public StorageBlobsProjector(
        BlobServiceClient serviceClient,
        string containerName,
        IOptions<JsonSerializerOptions>? options = null,
        ITypeMapper? mapper = null,
        StorageBlobProjectorOptions<T>? projectorOptions = null
    ) : this(serviceClient.GetBlobContainerClient(containerName), projectorOptions, options, mapper) { }

    /// <summary>Registers event handler with sync state update.</summary>
    /// <typeparam name="TEvent">Event type to handle.</typeparam>
    /// <param name="handler">State update function receiving current state and event.</param>
    /// <param name="getBlobId">Optional custom blob ID generator.</param>
    protected void On<TEvent>(Func<T, TEvent, T> handler, GetBlobId<TEvent>? getBlobId = null) where TEvent : class
        => On((ctx, state) => new ValueTask<T>(handler(state, ctx.Message)), getBlobId);

    /// <summary>Registers event handler with context and sync state update.</summary>
    /// <typeparam name="TEvent">Event type to handle.</typeparam>
    /// <param name="handler">State update function receiving context, current state, and event.</param>
    /// <param name="getBlobId">Optional custom blob ID generator.</param>
    protected void On<TEvent>(Func<IMessageConsumeContext<TEvent>, T, T> handler, GetBlobId<TEvent>? getBlobId = null) where TEvent : class
        => On((ctx, state) => new ValueTask<T>(handler(ctx, state)), getBlobId);

    /// <summary>Registers event handler with async state update.</summary>
    /// <typeparam name="TEvent">Event type to handle.</typeparam>
    /// <param name="handler">Async state update function receiving current state and event.</param>
    /// <param name="getBlobId">Optional custom blob ID generator.</param>
    protected void On<TEvent>(Func<T, TEvent, ValueTask<T>> handler, GetBlobId<TEvent>? getBlobId = null) where TEvent : class
        => On((ctx, state) => handler(state, ctx.Message), getBlobId);

    /// <summary>Registers event handler with context, async state update, and custom blob ID.</summary>
    /// <typeparam name="TEvent">Event type to handle.</typeparam>
    /// <param name="handler">Async state update function receiving context, current state, and event.</param>
    /// <param name="getBlobId">Optional custom blob ID generator.</param>
    protected void On<TEvent>(Func<IMessageConsumeContext<TEvent>, T, ValueTask<T>> handler, GetBlobId<TEvent>? getBlobId = null) where TEvent : class {
        if (!_handlers.TryAdd(typeof(TEvent), new Handler<TEvent>(this, handler, getBlobId).Handle)) {
            throw new ArgumentException($"Type {typeof(TEvent).Name} already has a handler");
        }

        if (!_map.TryGetTypeName<TEvent>(out _)) {
            Log.MessageTypeNotRegistered<TEvent>();
        }
    }

    private BlobClient GetBlobContainerClient(string blobName) => ContainerClient.GetBlobClient(blobName);

    /// <summary>Registers event handler without custom blob ID.</summary>
    /// <typeparam name="TEvent">Event type to handle.</typeparam>
    /// <param name="handler">Async state update function receiving context, current state, and event.</param>
    protected void On<TEvent>(Func<IMessageConsumeContext<TEvent>, T, ValueTask<T>> handler) where TEvent : class
        => On(handler, default);

    /// <summary>Handles incoming event by dispatching to registered handler.</summary>
    /// <param name="context">Event consume context.</param>
    /// <returns>Event handling status indicating success, failure, or ignore.</returns>
    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) =>
        _handlers.TryGetValue(context.Message!.GetType(), out var handler)
            ? await handler(context).NoContext()
            : EventHandlingStatus.Ignored;

    private T ToObjectFromJson(BinaryData content) => content.ToObjectFromJson<T>(_jsonOptions) ?? new T();
    private byte[] SerializeToUtf8Bytes(T updated) => JsonSerializer.SerializeToUtf8Bytes(updated, _jsonOptions);

    /// <summary>Gets blob name from ID and context. Can be overridden for custom naming.</summary>
    /// <param name="id">Blob identifier.</param>
    /// <param name="context">Event consume context.</param>
    /// <returns>Blob name as string.</returns>
    protected virtual string GetBlobName(string id, IMessageConsumeContext context) => GetBlobName(id);

    /// <summary>Gets blob name from ID. Default format: {id}/{T}.json</summary>
    /// <param name="id">Blob identifier.</param>
    /// <returns>Blob name as string.</returns>
    protected virtual string GetBlobName(string id) => $"{id}/{typeof(T).Name}.json";

    private class Handler<TEvent>
        where TEvent : class {
        private readonly StorageBlobsProjector<T> projector;
        private readonly Func<IMessageConsumeContext<TEvent>, T, ValueTask<T>> EventHandler;
        private readonly GetBlobId<TEvent>? GetBlobId;

        public Handler(StorageBlobsProjector<T> storageBlobsProjector, Func<IMessageConsumeContext<TEvent>, T, ValueTask<T>> handler, GetBlobId<TEvent>? getBlobId) {
            projector = storageBlobsProjector;
            EventHandler = handler;
            GetBlobId = getBlobId;
        }

        public async ValueTask<EventHandlingStatus> Handle(IMessageConsumeContext context) {
            var typedContext = context as MessageConsumeContext<TEvent> ?? new MessageConsumeContext<TEvent>(context);
            var blobId = GetBlobId == null
                ? context.Stream.GetId()
                : await GetBlobId(typedContext).NoContext();
            var blobName = projector.GetBlobName(blobId, typedContext);

            var blobClient = projector.GetBlobContainerClient(blobName);

            return await ModifyBlobWithRetries(projector._raceRetries).NoContext();

            async Task<EventHandlingStatus> ModifyBlobWithRetries(int retries) {
                try {
                    var status = await ModifyBlob().NoContext();
                    return status == EventHandlingStatus.Ignored ? EventHandlingStatus.Ignored : EventHandlingStatus.Success;
                } catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 409) {
                    return retries > 0 ? await ModifyBlobWithRetries(retries - 1).NoContext() : EventHandlingStatus.Failure;
                }
            }

            async Task<EventHandlingStatus> ModifyBlob() {
                try {
                    var blobContent = await blobClient.DownloadContentAsync(typedContext.CancellationToken).NoContext();

                    // Check idempotency if enabled
                    if (projector._idempotencyMode != IdempotencyMode.None) {
                        if (IsDuplicate(blobContent.Value.Details.Metadata)) {
                            return EventHandlingStatus.Ignored;
                        }
                    }

                    var content = blobContent.Value.Content;
                    var current = projector.Deserialize(content);

                    await UploadUpdated(current, new BlobRequestConditions { IfMatch = blobContent.Value.Details.ETag }).NoContext();
                    return EventHandlingStatus.Success;
                } catch (RequestFailedException ex) when (ex.Status == 404) {
                    // Blob doesn't exist, start with a new instance
                    await UploadUpdated(new T(), new BlobRequestConditions { IfNoneMatch = ETag.All }).NoContext();
                    return EventHandlingStatus.Success;
                }
            }

            bool IsDuplicate(IDictionary<string, string> metadata) {
                return projector._idempotencyMode switch {
                    IdempotencyMode.ByGlobalPosition =>
                        metadata.TryGetValue("GlobalPosition", out var storedPosition) &&
                        storedPosition == typedContext.GlobalPosition.ToString(),
                    IdempotencyMode.ByMessageId =>
                        metadata.TryGetValue("MessageId", out var storedId) &&
                        storedId == typedContext.MessageId,
                    _ => false
                };
            }

            async Task UploadUpdated(T current, BlobRequestConditions conditions) {
                var task = EventHandler(typedContext, current);
                var updated = task.IsCompletedSuccessfully
                    ? task.Result
                    : await task.NoContext();
                var json = projector.Serialize(updated);

                var uploadOptions = new BlobUploadOptions {
                    Conditions = conditions,
                    HttpHeaders = new BlobHttpHeaders {
                        ContentType = "application/json"
                    },
                    Metadata = new Dictionary<string, string> {
                        ["Stream"] = typedContext.Stream.ToString(),
                        ["MessageId"] = typedContext.MessageId,
                        ["StreamPosition"] = typedContext.StreamPosition.ToString(),
                        ["GlobalPosition"] = typedContext.GlobalPosition.ToString()
                    }
                };

                using var stream = new MemoryStream(json);
                var response = await blobClient.UploadAsync(stream, uploadOptions, typedContext.CancellationToken).NoContext();
            }
        }
    }
}