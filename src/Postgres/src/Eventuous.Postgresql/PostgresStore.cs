// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using System.Runtime.Serialization;
using System.Text;
using Eventuous.Diagnostics;
using Eventuous.Postgresql.Extensions;
using Npgsql;
using NpgsqlTypes;

// ReSharper disable ConvertClosureToMethodGroup

namespace Eventuous.Postgresql;

public delegate NpgsqlConnection GetPostgresConnection();

public record PostgresStoreOptions(string Schema = Schema.DefaultSchema);

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

    const string ContentType = "application/json";

    async Task<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken) {
        var connection = _getConnection();
        await connection.OpenAsync(cancellationToken).NoContext();
        connection.ReloadTypes();
        connection.TypeMapper.MapComposite<NewPersistedEvent>(_schema.StreamMessage);
        return connection;
    }

    public async Task<StreamEvent[]> ReadEvents(
        StreamName         stream,
        StreamReadPosition start,
        int                count,
        CancellationToken  cancellationToken
    ) {
        await using var connection = await OpenConnection(cancellationToken).NoContext();
        await using var cmd        = new NpgsqlCommand(_schema.ReadStreamForwards, connection);

        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("_stream_name", NpgsqlDbType.Varchar, stream.ToString());
        cmd.Parameters.AddWithValue("_from_position", NpgsqlDbType.Integer, start.Value);
        cmd.Parameters.AddWithValue("_count", NpgsqlDbType.Integer, count);

        try {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

            var result = reader.ReadEvents(cancellationToken);
            return await result.Select(x => ToStreamEvent(x)).ToArrayAsync(cancellationToken).NoContext();
        }
        catch (PostgresException e) when (e.MessageText.StartsWith("StreamNotFound")) {
            throw new StreamNotFound(stream);
        }
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

        NewPersistedEvent Convert(StreamEvent evt) {
            var data = _serializer.SerializeEvent(evt.Payload!);
            var meta = _metaSerializer.Serialize(evt.Metadata);
            return new NewPersistedEvent(evt.Id, data.EventType, AsString(data.Payload), AsString(meta));
        }

        string AsString(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);
    }

    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) {
        await using var connection = await OpenConnection(cancellationToken).NoContext();
        await using var cmd        = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = _schema.StreamExists;
        cmd.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, stream.ToString());
        var result = await cmd.ExecuteScalarAsync(cancellationToken).NoContext();
        return (bool)result!;
    }

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

    StreamEvent ToStreamEvent(PersistedEvent evt) {
        var deserialized = _serializer.DeserializeEvent(
            Encoding.UTF8.GetBytes(evt.JsonData),
            evt.MessageType,
            ContentType
        );

        var meta = evt.JsonMetadata == null
            ? new Metadata()
            : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return deserialized switch {
            SuccessfullyDeserialized success => AsStreamEvent(success.Payload),
            FailedToDeserialize failed => throw new SerializationException(
                $"Can't deserialize {evt.MessageType}: {failed.Error}"
            ),
            _ => throw new Exception("Unknown deserialization result")
        };

        StreamEvent AsStreamEvent(object payload)
            => new(evt.MessageId, payload, meta ?? new Metadata(), ContentType, evt.StreamPosition);
    }
}

record NewPersistedEvent(Guid MessageId, string MessageType, string JsonData, string? JsonMetadata);