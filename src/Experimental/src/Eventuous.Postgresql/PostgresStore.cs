// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using System.Text;
using System.Text.Json;
using Eventuous.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

// ReSharper disable ConvertClosureToMethodGroup

namespace Eventuous.Postgresql;

public delegate NpgsqlConnection GetPostgresConnection();

public record PostgresStoreOptions(string Schema = "eventuous");

public class PostgresStore : IEventStore {
    readonly GetPostgresConnection _getConnection;
    readonly IEventSerializer      _serializer;
    readonly IMetadataSerializer   _metaSerializer;
    readonly Schema                _schema;

    public PostgresStore(
        GetPostgresConnection getConnection,
        PostgresStoreOptions  options,
        IEventSerializer?     serializer     = null,
        IMetadataSerializer?  metaSerializer = null
    ) {
        _serializer     = serializer     ?? DefaultEventSerializer.Instance;
        _metaSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
        _getConnection  = Ensure.NotNull(getConnection, "Connection factory");
        _schema         = new Schema(options.Schema);
    }

    bool _initialized;

    async Task<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken) {
        var connection = _getConnection();
        await connection.OpenAsync(cancellationToken).NoContext();
        if (_initialized) return connection;

        connection.TypeMapper.MapComposite<PersistedEvent>(_schema.StreamMessage);
        _initialized = true;
        return connection;
    }

    public async Task<StreamEvent[]> ReadEvents(
        StreamName         stream,
        StreamReadPosition start,
        int                count,
        CancellationToken  cancellationToken
    ) {
        throw new NotImplementedException();
    }

    public async Task<AppendEventsResult> AppendEvents(
        StreamName                       stream,
        ExpectedStreamVersion            expectedVersion,
        IReadOnlyCollection<StreamEvent> events,
        CancellationToken                cancellationToken
    ) {
        var persistedEvents = events
            .Where(x => x.Payload != null)
            .Select(x => Convert(x))
            .ToArray();

        await using var connection  = await OpenConnection(cancellationToken).NoContext();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).NoContext();
        await using var cmd         = new NpgsqlCommand(_schema.AppendEvents, connection, transaction);

        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("_stream_name", NpgsqlDbType.Varchar, stream.ToString());
        cmd.Parameters.AddWithValue("_expected_version", NpgsqlDbType.Integer, expectedVersion.Value);
        cmd.Parameters.AddWithValue("_created", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("_messages", persistedEvents);

        try {
            AppendEventsResult result;

            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext()) {
                await reader.ReadAsync(cancellationToken).NoContext();
                result = new AppendEventsResult((ulong)reader.GetInt64(1), reader.GetInt32(0));
            }

            await transaction.CommitAsync(cancellationToken).NoContext();
            return result;
        }
        catch (PostgresException e) when (e.MessageText.StartsWith("WrongExpectedVersion")) {
            await transaction.RollbackAsync(cancellationToken).NoContext();
            EventuousEventSource.Log.UnableToAppendEvents(stream, e);
            throw new AppendToStreamException(stream, e);
        }

        PersistedEvent Convert(StreamEvent evt) {
            var data = _serializer.SerializeEvent(evt.Payload!);
            var meta = _metaSerializer.Serialize(evt.Metadata);
            return new PersistedEvent(evt.Id, data.EventType, AsString(data.Payload), AsString(meta));
        }

        string AsString(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);
    }

    public Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task TruncateStream(
        StreamName             stream,
        StreamTruncatePosition truncatePosition,
        ExpectedStreamVersion  expectedVersion,
        CancellationToken      cancellationToken
    )
        => throw new NotImplementedException();

    public Task DeleteStream(
        StreamName            stream,
        ExpectedStreamVersion expectedVersion,
        CancellationToken     cancellationToken
    )
        => throw new NotImplementedException();
}

record PersistedEvent(Guid MessageId, string MessageType, string JsonData, string? JsonMetadata);
