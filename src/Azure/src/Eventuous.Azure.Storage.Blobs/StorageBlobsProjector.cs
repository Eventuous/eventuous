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
    readonly BlobContainerClient _container;
    readonly JsonSerializerOptions _jsonOptions;
    readonly Dictionary<Type, Handler> _handlers = new();
    readonly ITypeMapper _map;
    public delegate ValueTask<T> Handler(IMessageConsumeContext context, T state);

    public StorageBlobsProjector(
        BlobContainerClient container,
        IOptions<JsonSerializerOptions>? options = null,
        ITypeMapper? mapper = null
    ) {
        _container = container;
        _jsonOptions = options?.Value ?? new(JsonSerializerOptions.Web);
        _map = mapper ?? TypeMap.Instance;
    }

    public StorageBlobsProjector(
        BlobServiceClient serviceClient,
        string containerName,
        IOptions<JsonSerializerOptions>? options = null,
        ITypeMapper? mapper = null
    ) : this(serviceClient.GetBlobContainerClient(containerName), options, mapper) { }

    protected void On<TEvent>(Func<T, TEvent, T> handler) where TEvent : class
        => On<TEvent>((ctx, state) => new ValueTask<T>(handler(state, ctx.Message)));

    protected void On<TEvent>(Func<IMessageConsumeContext<TEvent>, T, T> handler) where TEvent : class
        => On<TEvent>((ctx, state) => new ValueTask<T>(handler(ctx, state)));

    protected void On<TEvent>(Func<T, TEvent, ValueTask<T>> handler) where TEvent : class
        => On<TEvent>((ctx, state) => handler(state, ctx.Message));

    protected void On<TEvent>(Func<IMessageConsumeContext<TEvent>, T, ValueTask<T>> handler) where TEvent : class {
        if (!_handlers.TryAdd(typeof(TEvent), (context, state) => {
            var typedContext = context as MessageConsumeContext<TEvent> ?? new MessageConsumeContext<TEvent>(context);
            return handler(typedContext, state);
        })) {
            throw new ArgumentException($"Type {typeof(TEvent).Name} already has a handler");
        }

        if (!_map.TryGetTypeName<TEvent>(out _)) {
            Log.MessageTypeNotRegistered<TEvent>();
        }
    }

    public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) =>
        _handlers.TryGetValue(context.Message!.GetType(), out var handler)
            ? HandleInternal(context, handler)
            : new ValueTask<EventHandlingStatus>(EventHandlingStatus.Ignored);

    public async ValueTask<EventHandlingStatus> HandleInternal(IMessageConsumeContext context, Handler handler) {
        try {
            var blobName = GetBlobName(context.Stream, context);
            var blobClient = _container.GetBlobClient(blobName);

            try {
                BlobDownloadResult blobContent = await blobClient.DownloadContentAsync();

                var current = blobContent?.Content.ToObjectFromJson<T>(_jsonOptions) ?? new T();

                var uploadOptions = new BlobUploadOptions {
                    Conditions = new BlobRequestConditions { IfMatch = blobContent!.Details.ETag }
                };
                await UploadUpdated(blobClient, current, uploadOptions);
            } catch (RequestFailedException ex) when (ex.Status == 404) {
                // Blob doesn't exist, start with a new instance
                var insertOptions = new BlobUploadOptions {
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
                };
                await UploadUpdated(blobClient, new T(), insertOptions);
            }

            return EventHandlingStatus.Success;
        } catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 409) {
            return EventHandlingStatus.Ignored;
        }

        async Task UploadUpdated(BlobClient blobClient, T current, BlobUploadOptions uploadOptions) {
            var updated = await handler(context, current);
            var json = JsonSerializer.SerializeToUtf8Bytes(updated, _jsonOptions);

            using var stream = new MemoryStream(json);
            var response = await blobClient.UploadAsync(stream, uploadOptions, context.CancellationToken);
        }
    }

    protected virtual string GetBlobName(StreamName stream, IMessageConsumeContext context) => GetBlobName(stream.ToString());
    protected virtual string GetBlobName(string id) => $"{id}/{typeof(T).Name}.json";
}
