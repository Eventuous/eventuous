// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.Serialization;
using System.Text;
using Eventuous.Diagnostics;
using Eventuous.Postgresql.Extensions;
using Microsoft.Extensions.Options;

// ReSharper disable ConvertClosureToMethodGroup

namespace Eventuous.Postgresql;

public class PostgresStoreOptions {
    // ReSharper disable once ConvertToPrimaryConstructor
    public PostgresStoreOptions()
        => Schema = Postgresql.Schema.DefaultSchema;

    public PostgresStoreOptions(string schema)
        => Schema = schema;

    /// <summary>
    /// Override the default schema name.
    /// The property is mutable to allow using ASP.NET Core configuration.
    /// </summary>
    public string Schema { get; set; }
}

public class PostgresStore : IEventStore {
    readonly NpgsqlDataSource    _dataSource;
    readonly IEventSerializer    _serializer;
    readonly IMetadataSerializer _metaSerializer;
    readonly Schema              _schema;

    public PostgresStore(NpgsqlDataSource dataSource, PostgresStoreOptions? options, IEventSerializer? serializer = null, IMetadataSerializer? metaSerializer = null) {
        var pgOptions = options ?? new PostgresStoreOptions();
        _schema = new Schema(pgOptions.Schema);

        _serializer     = serializer     ?? DefaultEventSerializer.Instance;
        _metaSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
        _dataSource     = Ensure.NotNull(dataSource, "Data Source");
    }

    public PostgresStore(
        NpgsqlDataSource               dataSource,
        IOptions<PostgresStoreOptions> options,
        IEventSerializer?              serializer     = null,
        IMetadataSerializer?           metaSerializer = null
    ) : this(dataSource, options.Value, serializer, metaSerializer) { }

    const string ContentType = "application/json";

    public async Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken) {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).NoContext();

        await using var cmd = connection.GetCommand(_schema.ReadStreamForwards)
            .Add("_stream_name", NpgsqlDbType.Varchar, stream.ToString())
            .Add("_from_position", NpgsqlDbType.Integer, start.Value)
            .Add("_count", NpgsqlDbType.Integer, count);

        try {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

            var result = reader.ReadEvents(cancellationToken);
            return await result.Select(x => ToStreamEvent(x)).ToArrayAsync(cancellationToken).NoContext();
        }
        catch (PostgresException e) when (e.MessageText.StartsWith("StreamNotFound")) {
            throw new StreamNotFound(stream);
        }
    }

    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, int count, CancellationToken cancellationToken)
        => throw new NotImplementedException();

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

        await using var connection  = await _dataSource.OpenConnectionAsync(cancellationToken).NoContext();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).NoContext();

        await using var cmd = connection.GetCommand(_schema.AppendEvents, transaction)
            .Add("_stream_name", NpgsqlDbType.Varchar, stream.ToString())
            .Add("_expected_version", NpgsqlDbType.Integer, expectedVersion.Value)
            .Add("_created", DateTime.UtcNow)
            .Add("_messages", persistedEvents);

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
            PersistenceEventSource.Log.UnableToAppendEvents(stream, e);
            throw new AppendToStreamException(stream, e);
        }

        NewPersistedEvent Convert(StreamEvent evt) {
            var data = _serializer.SerializeEvent(evt.Payload!);
            var meta = _metaSerializer.Serialize(evt.Metadata);
            return new NewPersistedEvent(evt.Id, data.EventType, AsString(data.Payload), AsString(meta));
        }

        string AsString(ReadOnlySpan<byte> bytes)
            => Encoding.UTF8.GetString(bytes);
    }

    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).NoContext();

        await using var cmd = connection.GetCommand(_schema.StreamExists)
            .Add("name", NpgsqlDbType.Varchar, stream.ToString());

        var result = await cmd.ExecuteScalarAsync(cancellationToken).NoContext();
        return (bool)result!;
    }

    public Task TruncateStream(StreamName stream, StreamTruncatePosition truncatePosition, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    StreamEvent ToStreamEvent(PersistedEvent evt) {
        var deserialized = _serializer.DeserializeEvent(Encoding.UTF8.GetBytes(evt.JsonData), evt.MessageType, ContentType);

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
