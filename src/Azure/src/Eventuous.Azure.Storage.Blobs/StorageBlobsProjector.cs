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
    readonly Dictionary<Type, HandlerWithBlobId> _handlers = new();
    readonly ITypeMapper _map;
    protected readonly Func<BinaryData, T> Deserialize;
    protected readonly Func<T, byte[]> Serialize;
    public delegate ValueTask<T> Handler(IMessageConsumeContext context, T state, string blobName);
    public delegate ValueTask<string> GetBlobId<TEvent>(IMessageConsumeContext<TEvent> context) where TEvent : class;

    protected internal record HandlerWithBlobId(Handler Handler, Func<IMessageConsumeContext, ValueTask<string>>? GetBlobId);

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
        Func<IMessageConsumeContext, ValueTask<string>>? blobIdGetter = getBlobId != null
            ? async ctx => await getBlobId(new MessageConsumeContext<TEvent>(ctx))
            : null;
        
        if (!_handlers.TryAdd(typeof(TEvent), new HandlerWithBlobId(async (context, state, blobName) => {
            var typedContext = context as MessageConsumeContext<TEvent> ?? new MessageConsumeContext<TEvent>(context);
            return await handler(typedContext, state);
        }, blobIdGetter))) {
            throw new ArgumentException($"Type {typeof(TEvent).Name} already has a handler");
        }

        if (!_map.TryGetTypeName<TEvent>(out _)) {
            Log.MessageTypeNotRegistered<TEvent>();
        }
    }

    protected void On<TEvent>(Func<IMessageConsumeContext<TEvent>, T, ValueTask<T>> handler) where TEvent : class
        => On<TEvent>(handler, default);

    public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) =>
        _handlers.TryGetValue(context.Message!.GetType(), out var handlerInfo)
            ? HandleInternal(context, handlerInfo)
            : new ValueTask<EventHandlingStatus>(EventHandlingStatus.Ignored);

    protected async ValueTask<EventHandlingStatus> HandleInternal(IMessageConsumeContext context, HandlerWithBlobId handlerInfo) {
        try {
            string blobId;
            if (handlerInfo.GetBlobId != null) {
                blobId = await handlerInfo.GetBlobId(context);
            } else {
                blobId = context.Stream.ToString();
            }
            var blobName = GetBlobName(context.Stream, blobId);
            
            var blobClient = ContainerClient.GetBlobClient(blobName);

            try {
                BlobDownloadResult blobContent = await blobClient.DownloadContentAsync();

                var content = blobContent.Content;
                var current = Deserialize(content);

                var uploadOptions = new BlobUploadOptions {
                    Conditions = new BlobRequestConditions { IfMatch = blobContent!.Details.ETag }
                };
                await UploadUpdated(blobClient, current, uploadOptions, handlerInfo.Handler, blobName);
            } catch (RequestFailedException ex) when (ex.Status == 404) {
                // Blob doesn't exist, start with a new instance
                var insertOptions = new BlobUploadOptions {
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
                };
                await UploadUpdated(blobClient, new T(), insertOptions, handlerInfo.Handler, blobName);
            }

            return EventHandlingStatus.Success;
        } catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 409) {
            return EventHandlingStatus.Ignored;
        }

        async Task UploadUpdated(BlobClient blobClient, T current, BlobUploadOptions uploadOptions, Handler handler, string blobName) {
            var updated = await handler(context, current, blobName);
            var json = Serialize(updated);

            using var stream = new MemoryStream(json);
            var response = await blobClient.UploadAsync(stream, uploadOptions, context.CancellationToken);
        }
    }

    private T ToObjectFromJson(BinaryData content) => content.ToObjectFromJson<T>(_jsonOptions) ?? new T();
    private byte[] SerializeToUtf8Bytes(T updated) => JsonSerializer.SerializeToUtf8Bytes(updated, _jsonOptions);
    protected virtual string GetBlobName(StreamName stream, IMessageConsumeContext context) => GetBlobName(stream.ToString());
    protected virtual string GetBlobName(StreamName stream, string id) => GetBlobName($"{stream}/{id}.json");
    protected virtual string GetBlobName(string id) => $"{id}/{typeof(T).Name}.json";
}
