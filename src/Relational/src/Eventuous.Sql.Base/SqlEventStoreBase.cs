// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data.Common;
using System.Runtime.Serialization;
using System.Text;
using Eventuous.Diagnostics;
using static Eventuous.DeserializationResult;

namespace Eventuous.Sql.Base;

/// <summary>
/// Base class for SQL-based event stores
/// </summary>
/// <param name="serializer">Event serializer instance</param>
/// <param name="metaSerializer">Metadata serializer instance</param>
/// <typeparam name="TConnection">Database connection type</typeparam>
/// <typeparam name="TTransaction">Database transaction type</typeparam>
public abstract class SqlEventStoreBase<TConnection, TTransaction>(IEventSerializer? serializer, IMetadataSerializer? metaSerializer) : IEventStore
    where TConnection : DbConnection where TTransaction : DbTransaction {
    readonly IEventSerializer    _serializer     = serializer     ?? DefaultEventSerializer.Instance;
    readonly IMetadataSerializer _metaSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;

    const string ContentType = "application/json";

    /// <summary>
    /// Function to open a new database connection
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Open connection</returns>
    protected abstract ValueTask<TConnection> OpenConnection(CancellationToken cancellationToken);

    /// <summary>
    /// Get command to read events from the stream
    /// </summary>
    /// <param name="connection">Pre-opened connection</param>
    /// <param name="stream">Stream name</param>
    /// <param name="start">Starting position to read from</param>
    /// <param name="count">Number of events to read</param>
    /// <returns></returns>
    protected abstract DbCommand GetReadCommand(TConnection connection, StreamName stream, StreamReadPosition start, int count);

    /// <summary>
    /// Get command to read events from the stream in reverse order
    /// </summary>
    /// <param name="connection">Pre-opened connection</param>
    /// <param name="stream">Stream name</param>
    /// <param name="start">Starting position to read from</param>
    /// <param name="count">Number of events to read</param>
    /// <returns></returns>
    protected abstract DbCommand GetReadBackwardsCommand(TConnection connection, StreamName stream, StreamReadPosition start, int count);

    /// <summary>
    /// Get command to append events to the stream
    /// </summary>
    /// <param name="connection">Pre-opened connection</param>
    /// <param name="transaction">Started transaction</param>
    /// <param name="stream">Stream name</param>
    /// <param name="expectedVersion">Expected stream version</param>
    /// <param name="events">Events to append</param>
    /// <returns></returns>
    protected abstract DbCommand GetAppendCommand(
            TConnection           connection,
            TTransaction          transaction,
            StreamName            stream,
            ExpectedStreamVersion expectedVersion,
            NewPersistedEvent[]   events
        );

    /// <summary>
    /// Get command to check if the stream exists
    /// </summary>
    /// <param name="connection">Pre-opened connection</param>
    /// <param name="stream">Stream name</param>
    /// <returns>true if stream exists, otherwise false</returns>
    protected abstract DbCommand GetStreamExistsCommand(TConnection connection, StreamName stream);

    /// <summary>
    /// Get command to truncate stream at a given position
    /// </summary>
    /// <param name="connection">Pre-opened connection</param>
    /// <param name="stream">Stream name</param>
    /// <param name="expectedVersion">Expected current stream version</param>
    /// <param name="position">Truncation position</param>
    protected abstract DbCommand GetTruncateCommand(
            TConnection            connection,
            StreamName             stream,
            ExpectedStreamVersion  expectedVersion,
            StreamTruncatePosition position
        );

    /// <inheritdoc />
    public async Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, bool failIfNotFound, CancellationToken cancellationToken) {
        await using var connection = await OpenConnection(cancellationToken).NoContext();
        await using var cmd        = GetReadCommand(connection, stream, start, count);

        return await ReadInternal(cmd, stream, failIfNotFound, cancellationToken).NoContext();
    }

    /// <inheritdoc />
    public async Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, bool failIfNotFound, CancellationToken cancellationToken) {
        await using var connection = await OpenConnection(cancellationToken).NoContext();
        await using var cmd        = GetReadBackwardsCommand(connection, stream, start, count);

        return await ReadInternal(cmd, stream, failIfNotFound, cancellationToken).NoContext();
    }

    async Task<StreamEvent[]> ReadInternal(DbCommand cmd, StreamName stream, bool failIfNotFound, CancellationToken cancellationToken) {
        try {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

            var result = reader.ReadEvents(cancellationToken);

            return await result.Select(ToStreamEvent).ToArrayAsync(cancellationToken).NoContext();
        } catch (Exception e) {
            if (IsStreamNotFound(e)) {
                if (failIfNotFound) {
                    throw new StreamNotFound(stream);
                }

                return [];
            }

            throw;
        }
    }

    StreamEvent ToStreamEvent(PersistedEvent evt) {
        var deserialized = _serializer.DeserializeEvent(Encoding.UTF8.GetBytes(evt.JsonData), evt.MessageType, ContentType);

        var meta = evt.JsonMetadata == null ? new Metadata() : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return deserialized switch {
            SuccessfullyDeserialized success => AsStreamEvent(success.Payload),
            FailedToDeserialize failed       => throw new SerializationException($"Can't deserialize {evt.MessageType}: {failed.Error}"),
            _                                => throw new("Unknown deserialization result")
        };

        StreamEvent AsStreamEvent(object payload) => new(evt.MessageId, payload, meta ?? new Metadata(), ContentType, evt.StreamPosition);
    }

    /// <inheritdoc />
    public async Task<AppendEventsResult> AppendEvents(
            StreamName                          stream,
            ExpectedStreamVersion               expectedVersion,
            IReadOnlyCollection<NewStreamEvent> events,
            CancellationToken                   cancellationToken
        ) {
        var persistedEvents = events.Where(x => x.Payload != null).Select(Convert).ToArray();

        await using var connection  = await OpenConnection(cancellationToken).NoContext();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).NoContext();
        await using var cmd         = GetAppendCommand(connection, (TTransaction)transaction, stream, expectedVersion, persistedEvents);

        try {
            AppendEventsResult result;

            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext()) {
                await reader.ReadAsync(cancellationToken).NoContext();
                result = new((ulong)reader.GetInt64(1), reader.GetInt32(0));
            }

            await transaction.CommitAsync(cancellationToken).NoContext();

            return result;
        } catch (Exception e) {
            await transaction.RollbackAsync(cancellationToken).NoContext();
            PersistenceEventSource.Log.UnableToAppendEvents(stream, e);

            throw IsConflict(e) ? new AppendToStreamException(stream, e) : e;
        }

        NewPersistedEvent Convert(NewStreamEvent evt) {
            var data = _serializer.SerializeEvent(evt.Payload!);
            var meta = _metaSerializer.Serialize(evt.Metadata);

            return new(evt.Id, data.EventType, AsString(data.Payload), AsString(meta));
        }

        string AsString(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);
    }

    /// <inheritdoc />
    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) {
        await using var connection = await OpenConnection(cancellationToken).NoContext();
        await using var cmd        = GetStreamExistsCommand(connection, stream);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).NoContext();

        return (bool)result!;
    }

    /// <inheritdoc />
    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken) {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Checks if the exception indicates that the stream is not found
    /// </summary>
    /// <param name="exception">Exception returned by the database driver</param>
    /// <returns></returns>
    protected abstract bool IsStreamNotFound(Exception exception);

    /// <summary>
    /// Checks if the exception indicates a version conflict
    /// </summary>
    /// <param name="exception">Exception returned by the database driver</param>
    /// <returns></returns>
    protected abstract bool IsConflict(Exception exception);

    /// <inheritdoc />
    public async Task TruncateStream(
            StreamName             stream,
            StreamTruncatePosition truncatePosition,
            ExpectedStreamVersion  expectedVersion,
            CancellationToken      cancellationToken
        ) {
        await using var connection = await OpenConnection(cancellationToken).NoContext();
        await using var cmd        = GetTruncateCommand(connection, stream, expectedVersion, truncatePosition);

        await cmd.ExecuteScalarAsync(cancellationToken).NoContext();
    }
}

/// <summary>
/// Record representing a new persisted event
/// </summary>
/// <param name="MessageId">Unique message id</param>
/// <param name="MessageType">Message type string</param>
/// <param name="JsonData">Message payload as JSON</param>
/// <param name="JsonMetadata">Message metadata as JSON</param>
public record NewPersistedEvent(Guid MessageId, string MessageType, string JsonData, string? JsonMetadata);
