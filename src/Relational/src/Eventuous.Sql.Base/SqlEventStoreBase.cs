// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data.Common;
using System.Runtime.Serialization;
using System.Text;
using Eventuous.Diagnostics;

namespace Eventuous.Sql.Base;

public abstract class SqlEventStoreBase<TConnection, TTransaction>(IEventSerializer? serializer, IMetadataSerializer? metaSerializer) : IEventStore
    where TConnection : DbConnection where TTransaction : DbTransaction {
    readonly IEventSerializer    _serializer     = serializer     ?? DefaultEventSerializer.Instance;
    readonly IMetadataSerializer _metaSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;

    const string ContentType = "application/json";

    protected abstract ValueTask<TConnection> OpenConnection(CancellationToken cancellationToken);

    protected abstract DbCommand GetReadCommand(TConnection connection, StreamName stream, StreamReadPosition start, int count);

    protected abstract DbCommand GetReadBackwardsCommand(TConnection connection, StreamName stream, int count);

    protected abstract DbCommand GetAppendCommand(
            TConnection           connection,
            TTransaction          transaction,
            StreamName            stream,
            ExpectedStreamVersion expectedVersion,
            NewPersistedEvent[]   events
        );

    protected abstract DbCommand GetStreamExistsCommand(TConnection connection, StreamName stream);

    public async Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken) {
        await using var connection = await OpenConnection(cancellationToken).NoContext();
        await using var cmd        = GetReadCommand(connection, stream, start, count);

        return await ReadInternal(cmd, stream, cancellationToken).NoContext();
    }

    public async Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, int count, CancellationToken cancellationToken) {
        await using var connection = await OpenConnection(cancellationToken).NoContext();
        await using var cmd        = GetReadBackwardsCommand(connection, stream, count);

        return await ReadInternal(cmd, stream, cancellationToken).NoContext();
    }

    async Task<StreamEvent[]> ReadInternal(DbCommand cmd, StreamName stream, CancellationToken cancellationToken) {
        try {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

            var result = reader.ReadEvents(cancellationToken);

            return await result.Select(ToStreamEvent).ToArrayAsync(cancellationToken).NoContext();
        } catch (Exception e) {
            throw IsStreamNotFound(e) ? new StreamNotFound(stream) : e;
        }
    }

    StreamEvent ToStreamEvent(PersistedEvent evt) {
        var deserialized = _serializer.DeserializeEvent(Encoding.UTF8.GetBytes(evt.JsonData), evt.MessageType, ContentType);

        var meta = evt.JsonMetadata == null ? new Metadata() : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return deserialized switch {
            SuccessfullyDeserialized success => AsStreamEvent(success.Payload),
            FailedToDeserialize failed       => throw new SerializationException($"Can't deserialize {evt.MessageType}: {failed.Error}"),
            _                                => throw new Exception("Unknown deserialization result")
        };

        StreamEvent AsStreamEvent(object payload) => new(evt.MessageId, payload, meta ?? new Metadata(), ContentType, evt.StreamPosition);
    }

    public async Task<AppendEventsResult> AppendEvents(
            StreamName                       stream,
            ExpectedStreamVersion            expectedVersion,
            IReadOnlyCollection<StreamEvent> events,
            CancellationToken                cancellationToken
        ) {
        var persistedEvents = events
            .Where(x => x.Payload != null)
            .Select(Convert)
            .ToArray();

        await using var connection  = await OpenConnection(cancellationToken).NoContext();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).NoContext();
        await using var cmd         = GetAppendCommand(connection, (TTransaction)transaction, stream, expectedVersion, persistedEvents);

        try {
            AppendEventsResult result;

            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext()) {
                await reader.ReadAsync(cancellationToken).NoContext();
                result = new AppendEventsResult((ulong)reader.GetInt64(1), reader.GetInt32(0));
            }

            await transaction.CommitAsync(cancellationToken).NoContext();

            return result;
        } catch (Exception e) {
            await transaction.RollbackAsync(cancellationToken).NoContext();
            PersistenceEventSource.Log.UnableToAppendEvents(stream, e);

            throw IsConflict(e) ? new AppendToStreamException(stream, e) : e;
        }

        NewPersistedEvent Convert(StreamEvent evt) {
            var data = _serializer.SerializeEvent(evt.Payload!);
            var meta = _metaSerializer.Serialize(evt.Metadata);

            return new NewPersistedEvent(evt.Id, data.EventType, AsString(data.Payload), AsString(meta));
        }

        string AsString(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);
    }

    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) {
        await using var connection = await OpenConnection(cancellationToken).NoContext();
        await using var cmd        = GetStreamExistsCommand(connection, stream);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).NoContext();

        return (bool)result!;
    }

    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken) {
        throw new NotImplementedException();
    }

    protected abstract bool IsStreamNotFound(Exception exception);

    protected abstract bool IsConflict(Exception exception);

    public Task TruncateStream(
            StreamName             stream,
            StreamTruncatePosition truncatePosition,
            ExpectedStreamVersion  expectedVersion,
            CancellationToken      cancellationToken
        ) {
        throw new NotImplementedException();
    }
}

public record NewPersistedEvent(Guid MessageId, string MessageType, string JsonData, string? JsonMetadata);
