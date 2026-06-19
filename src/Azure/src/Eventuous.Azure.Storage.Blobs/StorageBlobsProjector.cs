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
    readonly Dictionary<Type, Func<IMessageConsumeContext, T, ValueTask<T>>> _handlers = new();
    readonly ITypeMapper _map;

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

    protected void On<TEvent>(Func<T, T> handler) where TEvent : class
        => On<TEvent>((ctx, state) => new ValueTask<T>(handler(state)));

    protected void On<TEvent>(Func<IMessageConsumeContext, T, T> handler) where TEvent : class
        => On<TEvent>((ctx, state) => new ValueTask<T>(handler(ctx, state)));

    protected void On<TEvent>(Func<T, ValueTask<T>> handler) where TEvent : class
        => On<TEvent>((ctx, state) => handler(state));

    protected void On<TEvent>(Func<IMessageConsumeContext, T, ValueTask<T>> wrapped) where TEvent : class {
        if (!_handlers.TryAdd(typeof(TEvent), wrapped)) {
            throw new ArgumentException($"Type {typeof(TEvent).Name} already has a handler");
        }

        if (!_map.TryGetTypeName<TEvent>(out _)) {
            Log.MessageTypeNotRegistered<TEvent>();
        }
    }

    protected virtual ValueTask<Func<IMessageConsumeContext, T, ValueTask<T>>> GetUpdate(IMessageConsumeContext context)
        => NoOp;

    ValueTask<Func<IMessageConsumeContext, T, ValueTask<T>>> NoOp => new((Func<IMessageConsumeContext, T, ValueTask<T>>?)null!);

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        var updateTask = _handlers.TryGetValue(context.Message!.GetType(), out var handler)
            ? new ValueTask<Func<IMessageConsumeContext, T, ValueTask<T>>>(handler)
            : GetUpdate(context);

        var update = updateTask.IsCompletedSuccessfully
            ? updateTask.Result
            : await updateTask.NoContext();

        if (update == null) {
            return EventHandlingStatus.Ignored;
        }

        var result = await HandleInternal(context, update);
        return result;
    }

    public async ValueTask<EventHandlingStatus> HandleInternal(IMessageConsumeContext context, Func<IMessageConsumeContext, T, ValueTask<T>> handler) {
        var blobName = GetBlobName(context.Stream, context);
        var blobClient = _container.GetBlobClient(blobName);

        BlobDownloadResult blobContent;
        ETag eTag;
        
        try {
            blobContent = await blobClient.DownloadContentAsync();
            eTag = blobContent.Details.ETag;
        } catch (RequestFailedException ex) when (ex.Status == 404) {
            // Blob doesn't exist, start with a new instance
            eTag = default;
            blobContent = default!;
        }

        var current = blobContent?.Content.ToObjectFromJson<T>(_jsonOptions) ?? new T();

        var updated = await handler(context, current);

        var json = JsonSerializer.SerializeToUtf8Bytes(updated, _jsonOptions);

        using var stream = new MemoryStream(json);
        
        if (eTag == default) {
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: context.CancellationToken);
            return EventHandlingStatus.Success;
        }
        
        try {
            var response = await blobClient.UploadAsync(stream, new BlobUploadOptions {
                Conditions = new BlobRequestConditions { IfMatch = eTag }
            }, context.CancellationToken);
            return EventHandlingStatus.Success;
        } catch (RequestFailedException ex) when (ex.Status == 412) {
            return EventHandlingStatus.Ignored;
        }
    }

    protected virtual string GetBlobName(StreamName stream, IMessageConsumeContext context) => $"{stream.GetId()}/{typeof(T).Name}.json";
}
