// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable CoVariantArrayConversion

using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Eventuous.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using static Eventuous.DeserializationResult;
using static Eventuous.Diagnostics.PersistenceEventSource;

namespace Eventuous.KurrentDB;

/// <summary>
/// EventStoreDB implementation of <see cref="IEventStore"/>
/// </summary>
[Obsolete("Use KurrentDBEventStore instead")]
public class EsdbEventStore : KurrentDBEventStore {
    /// <summary>
    /// Initialize the event store with the given client
    /// </summary>
    /// <param name="client">EventStoreDB client instance</param>
    /// <param name="serializer">Optional event serializer. When not provided, the default serializer will be used.</param>
    /// <param name="metaSerializer">Optional metadata serializer. When not provided, the default serializer will be used.</param>
    /// <param name="logger">Optional logger</param>
    public EsdbEventStore(
            KurrentDBClient               client,
            IEventSerializer?             serializer     = null,
            IMetadataSerializer?          metaSerializer = null,
            ILogger<KurrentDBEventStore>? logger         = null
        ) : base(
        client,
        serializer,
        metaSerializer,
        logger
    ) { }

    /// <summary>
    /// Initialize the event store with the given client settings. Will create the client instance.
    /// </summary>
    /// <param name="clientSettings">Client settings to be used to create a new client instance</param>
    /// <param name="serializer">Optional event serializer. When not provided, the default serializer will be used.</param>
    /// <param name="metaSerializer">Optional metadata serializer. When not provided, the default serializer will be used.</param>
    /// <param name="logger">Optional logger</param>
    public EsdbEventStore(
            KurrentDBClientSettings       clientSettings,
            IEventSerializer?             serializer     = null,
            IMetadataSerializer?          metaSerializer = null,
            ILogger<KurrentDBEventStore>? logger         = null
        ) : base(
        clientSettings,
        serializer,
        metaSerializer,
        logger
    ) { }
}

/// <summary>
/// EventStoreDB implementation of <see cref="IEventStore"/>
/// </summary>
public partial class KurrentDBEventStore : IEventStore {
    readonly ILogger<KurrentDBEventStore> _logger;
    readonly KurrentDBClient              _client;
    readonly IEventSerializer             _serializer;
    readonly IMetadataSerializer          _metaSerializer;

    /// <summary>
    /// Initialize the event store with the given client
    /// </summary>
    /// <param name="client">EventStoreDB client instance</param>
    /// <param name="serializer">Optional event serializer. When not provided, the default serializer will be used.</param>
    /// <param name="metaSerializer">Optional metadata serializer. When not provided, the default serializer will be used.</param>
    /// <param name="logger">Optional logger</param>
    public KurrentDBEventStore(
            KurrentDBClient               client,
            IEventSerializer?             serializer     = null,
            IMetadataSerializer?          metaSerializer = null,
            ILogger<KurrentDBEventStore>? logger         = null
        ) {
        _client         = Ensure.NotNull(client);
        _serializer     = serializer     ?? DefaultEventSerializer.Instance;
        _metaSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
        _logger         = logger         ?? NullLogger<KurrentDBEventStore>.Instance;
    }

    /// <summary>
    /// Initialize the event store with the given client settings. Will create the client instance.
    /// </summary>
    /// <param name="clientSettings">Client settings to be used to create a new client instance</param>
    /// <param name="serializer">Optional event serializer. When not provided, the default serializer will be used.</param>
    /// <param name="metaSerializer">Optional metadata serializer. When not provided, the default serializer will be used.</param>
    /// <param name="logger">Optional logger</param>
    public KurrentDBEventStore(
            KurrentDBClientSettings       clientSettings,
            IEventSerializer?             serializer     = null,
            IMetadataSerializer?          metaSerializer = null,
            ILogger<KurrentDBEventStore>? logger         = null
        ) : this(new KurrentDBClient(Ensure.NotNull(clientSettings)), serializer, metaSerializer, logger) { }

    /// <inheritdoc/>
    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken = default) {
        var read = _client.ReadStreamAsync(Direction.Backwards, stream, StreamPosition.End, 1, cancellationToken: cancellationToken);

        using var readState = read.ReadState;

        var state = await readState.NoContext();

        return state == ReadState.Ok;
    }

    /// <inheritdoc/>
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public Task<AppendEventsResult> AppendEvents(
            StreamName                          stream,
            ExpectedStreamVersion               expectedVersion,
            IReadOnlyCollection<NewStreamEvent> events,
            CancellationToken                   cancellationToken = default
        ) {
        var proposedEvents = events.Select(ToEventData);

        var deadline = TimeSpan.FromSeconds(60);

        var resultTask = _client.AppendToStreamAsync(stream, ToStreamState(expectedVersion), proposedEvents, deadline: deadline, cancellationToken: cancellationToken);

        return TryExecute(
            async () => {
                var result = await resultTask.NoContext();

                return new AppendEventsResult(result.LogPosition.CommitPosition, result.NextExpectedStreamState.ToInt64());
            },
            stream,
            true,
            () => new("Unable to appends events to {Stream}", stream),
            (s, ex) => {
                Log.UnableToAppendEvents(stream, ex);

                return new AppendToStreamException(s, ex);
            }
        );

        [RequiresDynamicCode("Calls Eventuous.IEventSerializer.SerializeEvent(Object)")]
        [RequiresUnreferencedCode("Calls Eventuous.IEventSerializer.SerializeEvent(Object)")]
        EventData ToEventData(NewStreamEvent streamEvent) {
            var (eventType, contentType, payload) = _serializer.SerializeEvent(streamEvent.Payload!);

            return new(
                Uuid.FromGuid(streamEvent.Id),
                eventType,
                payload,
                _metaSerializer.Serialize(streamEvent.Metadata),
                contentType
            );
        }
    }

    /// <inheritdoc/>
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public async Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, bool failIfNotFound, CancellationToken cancellationToken = default) {
        var read = _client.ReadStreamAsync(Direction.Forwards, stream, start.AsStreamPosition(), count, cancellationToken: cancellationToken);

        try {
            return await TryExecute(
                async () => {
                    var resolvedEvents = await read.ToArrayAsync(cancellationToken).NoContext();

                    return ToStreamEvents(resolvedEvents);
                },
                stream,
                failIfNotFound,
                () => new("Unable to read {Count} starting at {Start} events from {Stream}", count, start, stream),
                (s, ex) => new ReadFromStreamException(s, ex)
            );
        } catch (StreamNotFound) {
            if (failIfNotFound) {
                throw;
            }

            return [];
        }
    }

    /// <inheritdoc/>
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public async Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, bool failIfNotFound, CancellationToken cancellationToken = default) {
        var read = _client.ReadStreamAsync(
            Direction.Backwards,
            stream,
            start.AsStreamPosition(),
            count,
            resolveLinkTos: true,
            cancellationToken: cancellationToken
        );

        try {
            return await TryExecute(
                async () => {
                    var resolvedEvents = await read.ToArrayAsync(cancellationToken).NoContext();

                    return ToStreamEvents(resolvedEvents);
                },
                stream,
                failIfNotFound,
                () => new("Unable to read {Count} events backwards from {Stream}", count, stream),
                (s, ex) => new ReadFromStreamException(s, ex)
            );
        } catch (StreamNotFound) {
            if (failIfNotFound) {
                throw;
            }

            return [];
        }
    }

    /// <inheritdoc/>
    public Task TruncateStream(
            StreamName             stream,
            StreamTruncatePosition truncatePosition,
            ExpectedStreamVersion  expectedVersion,
            CancellationToken      cancellationToken
        ) {
        var meta = new StreamMetadata(truncateBefore: truncatePosition.AsStreamPosition());

        return TryExecute(
            () => _client.SetStreamMetadataAsync(stream, ToStreamState(expectedVersion), meta, cancellationToken: cancellationToken),
            stream,
            expectedVersion.ExistingStream,
            () => new("Unable to truncate stream {Stream} at {Position}", stream, truncatePosition),
            (s, ex) => new TruncateStreamException(s, ex)
        );
    }

    /// <inheritdoc/>
    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken = default)
        => TryExecute(
            () => _client.DeleteAsync(stream, ToStreamState(expectedVersion), cancellationToken: cancellationToken),
            stream,
            expectedVersion.ExistingStream,
            () => new("Unable to delete stream {Stream}", stream),
            (s, ex) => new DeleteStreamException(s, ex)
        );

    async Task<T> TryExecute<T>(
            Func<Task<T>>                      func,
            string                             stream,
            bool                               failIfNotFound,
            Func<ErrorInfo>                    getError,
            Func<string, Exception, Exception> getException
        ) {
        try {
            return await func().NoContext();
        } catch (StreamNotFoundException) {
            if (failIfNotFound) {
                LogStreamStreamNotFound(stream);
            }

            throw new StreamNotFound(stream);
        } catch (Exception ex) {
            var (message, args) = getError();
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
#pragma warning disable CA2254
            _logger.LogWarning(ex, message, args);
#pragma warning restore CA2254

            throw getException(stream, ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static StreamState ToStreamState(ExpectedStreamVersion version)
        => version == ExpectedStreamVersion.NoStream
            ? StreamState.NoStream
            : version == ExpectedStreamVersion.Any
                ? StreamState.Any
                : StreamState.StreamRevision((ulong)version.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    StreamEvent? ToStreamEvent(ResolvedEvent resolvedEvent) {
        var deserialized = _serializer.DeserializeEvent(
            resolvedEvent.Event.Data.Span,
            resolvedEvent.Event.EventType,
            resolvedEvent.Event.ContentType
        );

        return deserialized switch {
            SuccessfullyDeserialized success => AsStreamEvent(success.Payload),
            FailedToDeserialize failed       => HandleFailure(failed),
            _                                => throw new SerializationException("Unknown deserialization result")
        };

        StreamEvent? HandleFailure(FailedToDeserialize failed)
            => resolvedEvent.Event.EventType.StartsWith('$') ? null : throw new SerializationException($"Can't deserialize {resolvedEvent.Event.EventType}: {failed.Error}");

        Metadata? DeserializeMetadata() {
            var meta = resolvedEvent.Event.Metadata.Span;

            try {
                return meta.Length == 0 ? null : _metaSerializer.Deserialize(meta);
            } catch (MetadataDeserializationException e) {
                LogFailedToDeserializeMetadataAtStreamPosition(resolvedEvent.Event.EventStreamId, resolvedEvent.Event.EventNumber, e);

                return null;
            }
        }

        StreamEvent AsStreamEvent(object payload)
            => new(
                resolvedEvent.Event.EventId.ToGuid(),
                payload,
                DeserializeMetadata() ?? new Metadata(),
                resolvedEvent.Event.ContentType,
                resolvedEvent.Event.EventNumber.ToInt64()
            );
    }

    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    StreamEvent[] ToStreamEvents(ResolvedEvent[] resolvedEvents)
        => resolvedEvents
            .Select(ToStreamEvent)
            .Where(x => x != null)
            .Select(x => x!.Value)
            .ToArray();

    record ErrorInfo(string Message, params object[] Args);

    [LoggerMessage(LogLevel.Warning, "Stream {stream} not found")]
    partial void LogStreamStreamNotFound(string stream);

    [LoggerMessage(LogLevel.Warning, "Failed to deserialize metadata at {stream}:{position}")]
    partial void LogFailedToDeserializeMetadataAtStreamPosition(string stream, StreamPosition position, Exception ex);
}
