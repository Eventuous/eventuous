// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.Serialization;
using System.Text;
using Eventuous.Postgresql.Extensions;
using static Eventuous.DeserializationResult;

namespace Eventuous.Postgresql.Snapshots;

/// <summary>
/// PostgreSQL snapshot store implementation for storing snapshots separately from event streams.
/// </summary>
public class PostgresSnapshotStore : ISnapshotStore {
    readonly NpgsqlDataSource      _dataSource;
    readonly SnapshotSchema         _schema;
    readonly IEventSerializer       _serializer;
    const    string                 ContentType = "application/json";

    public PostgresSnapshotStore(
            NpgsqlDataSource           dataSource,
            PostgresSnapshotStoreOptions? options,
            IEventSerializer?           serializer = null
        ) {
        var pgOptions = options ?? new PostgresSnapshotStoreOptions();
        _schema     = new SnapshotSchema(pgOptions.Schema);
        _dataSource = Ensure.NotNull(dataSource, "Data Source");
        _serializer = serializer ?? DefaultEventSerializer.Instance;
    }

    /// <inheritdoc />
    [RequiresDynamicCode("Only works with AOT when using DefaultStaticEventSerializer")]
    [RequiresUnreferencedCode("Only works with AOT when using DefaultStaticEventSerializer")]
    public async Task<Snapshot?> Read(StreamName streamName, CancellationToken cancellationToken = default) {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).NoContext();
        await using var cmd        = connection.GetCommand(_schema.ReadSnapshot)
            .Add("stream_name", NpgsqlDbType.Varchar, streamName.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

        if (!await reader.ReadAsync(cancellationToken).NoContext()) {
            return null;
        }

        var revision   = reader.GetInt64(0);
        var eventType  = reader.GetString(1);
        var jsonData   = reader.GetString(2);

        var deserialized = _serializer.DeserializeEvent(
            Encoding.UTF8.GetBytes(jsonData),
            eventType,
            ContentType
        );

        return deserialized switch {
            SuccessfullyDeserialized success => new Snapshot {
                Revision = revision,
                Payload  = success.Payload
            },
            FailedToDeserialize failed => throw new SerializationException($"Can't deserialize snapshot {eventType}: {failed.Error}"),
            _                          => throw new("Unknown deserialization result")
        };
    }

    /// <inheritdoc />
    [RequiresDynamicCode("Only works with AOT when using DefaultStaticEventSerializer")]
    [RequiresUnreferencedCode("Only works with AOT when using DefaultStaticEventSerializer")]
    public async Task Write(StreamName streamName, Snapshot snapshot, CancellationToken cancellationToken = default) {
        if (snapshot.Payload == null) {
            throw new ArgumentException("Snapshot payload cannot be null", nameof(snapshot));
        }

        var serialized = _serializer.SerializeEvent(snapshot.Payload);
        var jsonData   = Encoding.UTF8.GetString(serialized.Payload);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).NoContext();
        await using var cmd        = connection.GetCommand(_schema.WriteSnapshot)
            .Add("stream_name", NpgsqlDbType.Varchar, streamName.ToString())
            .Add("revision", NpgsqlDbType.Bigint, snapshot.Revision)
            .Add("event_type", NpgsqlDbType.Varchar, serialized.EventType)
            .Add("json_data", NpgsqlDbType.Jsonb, jsonData);

        await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
    }

    /// <inheritdoc />
    public async Task Delete(StreamName streamName, CancellationToken cancellationToken = default) {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).NoContext();
        await using var cmd        = connection.GetCommand(_schema.DeleteSnapshot)
            .Add("stream_name", NpgsqlDbType.Varchar, streamName.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
    }
}

