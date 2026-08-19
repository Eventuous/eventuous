// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Azure;
using Azure.Storage.Blobs.Models;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Logging;
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
/// The blob container must exist; the projector doesn't create it.
/// </para>
/// </remarks>
public class BlobStorageProjector<T> : BaseEventHandler where T : class, new() {
    /// <summary>Azure Blob Storage container client.</summary>
    protected readonly BlobContainerClient ContainerClient;

    readonly JsonSerializerOptions                                                          _jsonOptions;
    readonly Dictionary<Type, Func<IMessageConsumeContext, ValueTask<EventHandlingStatus>>> _handlers = new();
    readonly ITypeMapper                                                                    _map;
    readonly int                                                                            _raceRetries;
    readonly IdempotencyMode                                                                _idempotencyMode;

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
    /// <param name="mapper">Optional type mapper for event type resolution.</param>
    public BlobStorageProjector(BlobContainerClient container, BlobStorageProjectorOptions? projectorOptions = null, ITypeMapper? mapper = null) {
        ContainerClient  = container;
        _jsonOptions     = new(projectorOptions?.JsonOptions ?? JsonSerializerOptions.Web);
        _map             = mapper ?? TypeMap.Instance;
        _raceRetries     = projectorOptions?.RaceRetries ?? 0;
        _idempotencyMode = projectorOptions?.IdempotencyMode ?? IdempotencyMode.None;
    }

    /// <summary>
    /// Initializes projector with service client and container name.
    /// </summary>
    /// <param name="serviceClient">Azure Blob Storage service client.</param>
    /// <param name="containerName">Name of the container to use.</param>
    /// <param name="projectorOptions">Optional projector configuration.</param>
    /// <param name="mapper">Optional type mapper for event type resolution.</param>
    public BlobStorageProjector(
        BlobServiceClient            serviceClient,
        string                       containerName,
        BlobStorageProjectorOptions? projectorOptions = null,
        ITypeMapper?                 mapper           = null
    ) : this(serviceClient.GetBlobContainerClient(containerName), projectorOptions, mapper) { }

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

    /// <summary>Handles incoming event by dispatching to registered handler.</summary>
    /// <param name="context">Event consume context.</param>
    /// <returns>Event handling status indicating success, failure, or ignore.</returns>
    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) =>
        _handlers.TryGetValue(context.Message!.GetType(), out var handler)
            ? await handler(context).NoContext()
            : EventHandlingStatus.Ignored;

    T ToObjectFromJson(BinaryData content) => content.ToObjectFromJson<T>(_jsonOptions) ?? new T();

    byte[] SerializeToUtf8Bytes(T updated) => JsonSerializer.SerializeToUtf8Bytes(updated, _jsonOptions);

    /// <summary>Gets blob name from ID and context. Can be overridden for custom naming.</summary>
    /// <param name="id">Blob identifier.</param>
    /// <param name="context">Event consume context.</param>
    /// <returns>Blob name as string.</returns>
    protected virtual string GetBlobName(string id, IMessageConsumeContext context) => GetBlobName(id);

    /// <summary>Gets blob name from ID. Default format: {id}/{T}.json</summary>
    /// <param name="id">Blob identifier.</param>
    /// <returns>Blob name as string.</returns>
    protected virtual string GetBlobName(string id) => $"{id}/{typeof(T).Name}.json";

    class Handler<TEvent>(BlobStorageProjector<T> projector, Func<IMessageConsumeContext<TEvent>, T, ValueTask<T>> eventHandler, GetBlobId<TEvent>? getBlobId)
        where TEvent : class {
        bool _warnedZeroGlobalPosition;

        public async ValueTask<EventHandlingStatus> Handle(IMessageConsumeContext context) {
            var typedContext = context as MessageConsumeContext<TEvent> ?? new MessageConsumeContext<TEvent>(context);

            if (projector._idempotencyMode == IdempotencyMode.ByGlobalPosition && context.GlobalPosition == 0 && !_warnedZeroGlobalPosition) {
                _warnedZeroGlobalPosition = true;

                Logger.Current?.WarnLog?.Log(
                    "ByGlobalPosition idempotency requires events with real global positions, but an event arrived with global position 0. Subsequent events may be treated as duplicates and ignored. Use ByMessageId for message broker subscriptions."
                );
            }

            var blobId = getBlobId == null
                ? context.Stream.GetId()
                : await getBlobId(typedContext).NoContext();
            var blobName = projector.GetBlobName(blobId, typedContext);

            var blobClient = projector.ContainerClient.GetBlobClient(blobName);

            var retries = projector._raceRetries;

            while (true) {
                Response<BlobDownloadResult>? blobContent;

                try {
                    blobContent = await blobClient.DownloadContentAsync(typedContext.CancellationToken).NoContext();
                } catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == BlobErrorCode.BlobNotFound.ToString()) {
                    // Blob doesn't exist, start with a new instance
                    blobContent = null;
                }

                T                     current;
                BlobRequestConditions conditions;

                if (blobContent == null) {
                    current    = new T();
                    conditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
                } else {
                    // Check idempotency if enabled
                    if (projector._idempotencyMode != IdempotencyMode.None && IsDuplicate(blobContent.Value.Details.Metadata)) {
                        return EventHandlingStatus.Ignored;
                    }

                    current    = projector.ToObjectFromJson(blobContent.Value.Content);
                    conditions = new BlobRequestConditions { IfMatch = blobContent.Value.Details.ETag };
                }

                // The user-supplied handler and the user-configurable JSON serialization run outside
                // the catch blocks, so their own Azure exceptions are never mistaken for blob races
                var updated = await eventHandler(typedContext, current).NoContext();
                var json    = projector.SerializeToUtf8Bytes(updated);

                var uploadOptions = new BlobUploadOptions {
                    Conditions = conditions,
                    HttpHeaders = new BlobHttpHeaders {
                        ContentType = "application/json"
                    },
                    // Azure requires metadata values to be ASCII, while stream names and message ids
                    // can be arbitrary strings, so they are stored percent-encoded
                    Metadata = new Dictionary<string, string> {
                        ["Stream"]         = Uri.EscapeDataString(typedContext.Stream.ToString()),
                        ["MessageId"]      = Uri.EscapeDataString(typedContext.MessageId),
                        ["StreamPosition"] = typedContext.StreamPosition.ToString(),
                        ["GlobalPosition"] = typedContext.GlobalPosition.ToString()
                    }
                };

                try {
                    using var stream = new MemoryStream(json);
                    await blobClient.UploadAsync(stream, uploadOptions, typedContext.CancellationToken).NoContext();

                    return EventHandlingStatus.Success;
                } catch (RequestFailedException ex) when (IsConcurrencyConflict(ex)) {
                    // Lost the optimistic concurrency race: re-read the state and try again
                    if (retries-- <= 0) return EventHandlingStatus.Failure;
                }
            }

            bool IsDuplicate(IDictionary<string, string> metadata) => projector._idempotencyMode switch {
                IdempotencyMode.ByGlobalPosition =>
                    metadata.TryGetValue("GlobalPosition", out var storedPosition) &&
                    ulong.TryParse(storedPosition, out var currentGlobalPosition) &&
                    typedContext.GlobalPosition <= currentGlobalPosition,
                IdempotencyMode.ByMessageId =>
                    metadata.TryGetValue("MessageId", out var storedId) &&
                    storedId == Uri.EscapeDataString(typedContext.MessageId),
                _ => false
            };
        }

        static bool IsConcurrencyConflict(RequestFailedException ex)
            => ex.Status is 412 or 409 &&
                (ex.ErrorCode == BlobErrorCode.ConditionNotMet.ToString() || ex.ErrorCode == BlobErrorCode.BlobAlreadyExists.ToString());
    }
}
