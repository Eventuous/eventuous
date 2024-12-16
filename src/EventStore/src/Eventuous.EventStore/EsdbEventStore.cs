// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable CoVariantArrayConversion

using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Eventuous.Tools;
using static Eventuous.DeserializationResult;
using static Eventuous.Diagnostics.PersistenceEventSource;

namespace Eventuous.EventStore;

/// <summary>
/// EventStoreDB implementation of <see cref="IEventStore"/>
/// </summary>
public class EsdbEventStore : IEventStore {
    readonly ILogger<EsdbEventStore>? _logger;
    readonly EventStoreClient         _client;
    readonly IEventSerializer         _serializer;
    readonly IMetadataSerializer      _metaSerializer;

    /// <summary>
    /// Initialize the event store with the given client
    /// </summary>
    /// <param name="client">EventStoreDB client instance</param>
    /// <param name="serializer">Optional event serializer. When not provided, the default serializer will be used.</param>
    /// <param name="metaSerializer">Optional metadata serializer. When not provided, the default serializer will be used.</param>
    /// <param name="logger">Optional logger</param>
    public EsdbEventStore(
            EventStoreClient         client,
            IEventSerializer?        serializer     = null,
            IMetadataSerializer?     metaSerializer = null,
            ILogger<EsdbEventStore>? logger         = null
        ) {
        _logger         = logger;
        _client         = Ensure.NotNull(client);
        _serializer     = serializer     ?? DefaultEventSerializer.Instance;
        _metaSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
    }

    /// <summary>
    /// Initialize the event store with the given client settings. Will create the client instance.
    /// </summary>
    /// <param name="clientSettings">Client settings to be used to create a new client instance</param>
    /// <param name="serializer">Optional event serializer. When not provided, the default serializer will be used.</param>
    /// <param name="metaSerializer">Optional metadata serializer. When not provided, the default serializer will be used.</param>
    /// <param name="logger">Optional logger</param>
    public EsdbEventStore(
            EventStoreClientSettings clientSettings,
            IEventSerializer?        serializer     = null,
            IMetadataSerializer?     metaSerializer = null,
            ILogger<EsdbEventStore>? logger         = null
        ) : this(new EventStoreClient(Ensure.NotNull(clientSettings)), serializer, metaSerializer, logger) { }

    /// <inheritdoc/>
    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken = default) {
        var read = _client.ReadStreamAsync(Direction.Backwards, stream, StreamPosition.End, 1, cancellationToken: cancellationToken);

        using var readState = read.ReadState;

        var state = await readState.NoContext();

        return state == ReadState.Ok;
    }

    /// <inheritdoc/>
    public Task<AppendEventsResult> AppendEvents(
            StreamName                          stream,
            ExpectedStreamVersion               expectedVersion,
            IReadOnlyCollection<NewStreamEvent> events,
            CancellationToken                   cancellationToken = default
        ) {
        var proposedEvents = events.Select(ToEventData);

        var resultTask = expectedVersion == ExpectedStreamVersion.NoStream
            ? _client.AppendToStreamAsync(stream, StreamState.NoStream, proposedEvents, cancellationToken: cancellationToken)
            : AnyOrNot(
                expectedVersion,
                () => _client.AppendToStreamAsync(stream, StreamState.Any, proposedEvents, cancellationToken: cancellationToken),
                () => _client.AppendToStreamAsync(stream, expectedVersion.AsStreamRevision(), proposedEvents, cancellationToken: cancellationToken)
            );

        return TryExecute(
            async () => {
                var result = await resultTask.NoContext();

                return new AppendEventsResult(result.LogPosition.CommitPosition, result.NextExpectedStreamRevision.ToInt64());
            },
            stream,
            () => new("Unable to appends events to {Stream}", stream),
            (s, ex) => {
                Log.UnableToAppendEvents(stream, ex);

                return new AppendToStreamException(s, ex);
            }
        );

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
    public Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken = default) {
        var read = _client.ReadStreamAsync(Direction.Forwards, stream, start.AsStreamPosition(), count, cancellationToken: cancellationToken);

        return TryExecute(
            async () => {
                var resolvedEvents = await read.ToArrayAsync(cancellationToken).NoContext();

                return ToStreamEvents(resolvedEvents);
            },
            stream,
            () => new("Unable to read {Count} starting at {Start} events from {Stream}", count, start, stream),
            (s, ex) => new ReadFromStreamException(s, ex)
        );
    }

    /// <inheritdoc/>
    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken = default) {
        var read = _client.ReadStreamAsync(
            Direction.Backwards,
            stream,
            start.AsStreamPosition(),
            count,
            resolveLinkTos: true,
            cancellationToken: cancellationToken
        );

        return TryExecute(
            async () => {
                var resolvedEvents = await read.ToArrayAsync(cancellationToken).NoContext();

                return ToStreamEvents(resolvedEvents);
            },
            stream,
            () => new("Unable to read {Count} events backwards from {Stream}", count, stream),
            (s, ex) => new ReadFromStreamException(s, ex)
        );
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
            () => AnyOrNot(
                expectedVersion,
                () => _client.SetStreamMetadataAsync(stream, StreamState.Any, meta, cancellationToken: cancellationToken),
                () => _client.SetStreamMetadataAsync(stream, expectedVersion.AsStreamRevision(), meta, cancellationToken: cancellationToken)
            ),
            stream,
            () => new("Unable to truncate stream {Stream} at {Position}", stream, truncatePosition),
            (s, ex) => new TruncateStreamException(s, ex)
        );
    }

    /// <inheritdoc/>
    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken = default)
        => TryExecute(
            () => AnyOrNot(
                expectedVersion,
                () => _client.DeleteAsync(stream, StreamState.Any, cancellationToken: cancellationToken),
                () => _client.DeleteAsync(stream, expectedVersion.AsStreamRevision(), cancellationToken: cancellationToken)
            ),
            stream,
            () => new("Unable to delete stream {Stream}", stream),
            (s, ex) => new DeleteStreamException(s, ex)
        );

    async Task<T> TryExecute<T>(
            Func<Task<T>>                      func,
            string                             stream,
            Func<ErrorInfo>                    getError,
            Func<string, Exception, Exception> getException
        ) {
        try {
            return await func().NoContext();
        } catch (StreamNotFoundException) {
            _logger?.LogWarning("Stream {Stream} not found", stream);

            throw new StreamNotFound(stream);
        } catch (Exception ex) {
            var (message, args) = getError();
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            _logger?.LogWarning(ex, message, args);

            throw getException(stream, ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Task<T> AnyOrNot<T>(ExpectedStreamVersion version, Func<Task<T>> whenAny, Func<Task<T>> otherwise)
        => version == ExpectedStreamVersion.Any ? whenAny() : otherwise();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    StreamEvent ToStreamEvent(ResolvedEvent resolvedEvent) {
        var deserialized = _serializer.DeserializeEvent(
            resolvedEvent.Event.Data.Span,
            resolvedEvent.Event.EventType,
            resolvedEvent.Event.ContentType
        );

        return deserialized switch {
            SuccessfullyDeserialized success => AsStreamEvent(success.Payload),
            FailedToDeserialize failed => throw new SerializationException(
                $"Can't deserialize {resolvedEvent.Event.EventType}: {failed.Error}"
            ),
            _ => throw new SerializationException("Unknown deserialization result")
        };

        Metadata? DeserializeMetadata() {
            var meta = resolvedEvent.Event.Metadata.Span;

            try {
                return meta.Length == 0 ? null : _metaSerializer.Deserialize(meta);
            } catch (MetadataDeserializationException e) {
                _logger?.LogWarning(
                    e,
                    "Failed to deserialize metadata at {Stream}:{Position}",
                    resolvedEvent.Event.EventStreamId,
                    resolvedEvent.Event.EventNumber
                );

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

    StreamEvent[] ToStreamEvents(ResolvedEvent[] resolvedEvents)
        => resolvedEvents
            .Where(x => !x.Event.EventType.StartsWith('$'))
            .Select(ToStreamEvent)
            .ToArray();

    record ErrorInfo(string Message, params object[] Args);
}
