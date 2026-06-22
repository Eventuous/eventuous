using Azure;
using Azure.Storage.Blobs.Models;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Microsoft.Extensions.Options;
using System.Text.Json;
using RequestFailedException = Azure.RequestFailedException;

using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Azure.Storage.Blobs;

public class StorageBlobsProjector<T> : BaseEventHandler where T : class, new() {
    protected readonly BlobContainerClient ContainerClient;
    readonly JsonSerializerOptions _jsonOptions;
    readonly Dictionary<Type, Func<IMessageConsumeContext, ValueTask>> _handlers = new();
    readonly ITypeMapper _map;
    protected readonly Func<BinaryData, T> Deserialize;
    protected readonly Func<T, byte[]> Serialize;
    public delegate ValueTask<string> GetBlobId<TEvent>(IMessageConsumeContext<TEvent> context) where TEvent : class;

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
    }

    public StorageBlobsProjector(
        BlobServiceClient serviceClient,
        string containerName,
        IOptions<JsonSerializerOptions>? options = null,
        ITypeMapper? mapper = null,
        StorageBlobProjectorOptions<T>? projectorOptions = null
    ) : this(serviceClient.GetBlobContainerClient(containerName), projectorOptions, options, mapper) { }

    protected void On<TEvent>(Func<T, TEvent, T> handler, GetBlobId<TEvent>? getBlobId = null) where TEvent : class
        => On((ctx, state) => new ValueTask<T>(handler(state, ctx.Message)), getBlobId);

    protected void On<TEvent>(Func<IMessageConsumeContext<TEvent>, T, T> handler, GetBlobId<TEvent>? getBlobId = null) where TEvent : class
        => On((ctx, state) => new ValueTask<T>(handler(ctx, state)), getBlobId);

    protected void On<TEvent>(Func<T, TEvent, ValueTask<T>> handler, GetBlobId<TEvent>? getBlobId = null) where TEvent : class
        => On((ctx, state) => handler(state, ctx.Message), getBlobId);

    protected void On<TEvent>(Func<IMessageConsumeContext<TEvent>, T, ValueTask<T>> handler, GetBlobId<TEvent>? getBlobId = null) where TEvent : class {
        if (!_handlers.TryAdd(typeof(TEvent), HandleInternal(handler, getBlobId))) {
            throw new ArgumentException($"Type {typeof(TEvent).Name} already has a handler");
        }

        if (!_map.TryGetTypeName<TEvent>(out _)) {
            Log.MessageTypeNotRegistered<TEvent>();
        }
    }

    private Func<IMessageConsumeContext, ValueTask> HandleInternal<TEvent>(Func<IMessageConsumeContext<TEvent>, T, ValueTask<T>> handler, GetBlobId<TEvent>? getBlobId) where TEvent : class => async context => {
        var typedContext = context as MessageConsumeContext<TEvent> ?? new MessageConsumeContext<TEvent>(context);
        var blobId = getBlobId == null
            ? context.Stream.GetId()
            : await getBlobId(typedContext);
        var blobName = GetBlobName(blobId, typedContext);

        var blobClient = ContainerClient.GetBlobClient(blobName);

        try {
            BlobDownloadResult blobContent = await blobClient.DownloadContentAsync();

            var content = blobContent.Content;
            var current = Deserialize(content);

            var uploadOptions = new BlobUploadOptions {
                Conditions = new BlobRequestConditions { IfMatch = blobContent!.Details.ETag }
            };
            await UploadUpdated(current, uploadOptions);
        } catch (RequestFailedException ex) when (ex.Status == 404) {
            // Blob doesn't exist, start with a new instance
            var insertOptions = new BlobUploadOptions {
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            };
            await UploadUpdated(new T(), insertOptions);
        }

        async Task UploadUpdated(T current, BlobUploadOptions uploadOptions) {
            var updated = await handler(typedContext, current);
            var json = Serialize(updated);

            using var stream = new MemoryStream(json);
            var response = await blobClient.UploadAsync(stream, uploadOptions, typedContext.CancellationToken);
        }
    };
    
    protected void On<TEvent>(Func<IMessageConsumeContext<TEvent>, T, ValueTask<T>> handler) where TEvent : class
        => On(handler, default);

    public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) =>
        _handlers.TryGetValue(context.Message!.GetType(), out var handler)
            ? HandleEventInternal(context, handler)
            : new ValueTask<EventHandlingStatus>(EventHandlingStatus.Ignored);

    protected async ValueTask<EventHandlingStatus> HandleEventInternal(IMessageConsumeContext context, Func<IMessageConsumeContext, ValueTask>  handler) {
        try {
            await handler(context);
            return EventHandlingStatus.Success;
        } catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 409) {
            return EventHandlingStatus.Ignored;
        }
    }

    private T ToObjectFromJson(BinaryData content) => content.ToObjectFromJson<T>(_jsonOptions) ?? new T();
    private byte[] SerializeToUtf8Bytes(T updated) => JsonSerializer.SerializeToUtf8Bytes(updated, _jsonOptions);
    protected virtual string GetBlobName(string id, IMessageConsumeContext context) => GetBlobName(id);
    protected virtual string GetBlobName(string id) => $"{id}/{typeof(T).Name}.json";
}
