// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Runtime.Serialization;
using System.Text;
using static Eventuous.Diagnostics.PersistenceEventSource;

namespace Eventuous.Redis;

using Tools;

public delegate IDatabase GetRedisDatabase();

public record RedisStoreOptions;

public class RedisStore : IEventReader, IEventWriter {
    readonly GetRedisDatabase    _getDatabase;
    readonly IEventSerializer    _serializer;
    readonly IMetadataSerializer _metaSerializer;

    public RedisStore(
        GetRedisDatabase     getDatabase,
        RedisStoreOptions    options,
        IEventSerializer?    serializer     = null,
        IMetadataSerializer? metaSerializer = null
    ) {
        _serializer     = serializer     ?? DefaultEventSerializer.Instance;
        _metaSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
        _getDatabase    = Ensure.NotNull(getDatabase, "Connection factory");
    }

    const string ContentType = "application/json";

    public async Task<StreamEvent[]> ReadEvents(
        StreamName         stream,
        StreamReadPosition start,
        int                count,
        CancellationToken  cancellationToken
    ) {
        var result = await _getDatabase().StreamReadAsync(stream.ToString(), start.Value.ToRedisValue(), count).NoContext();
        if (result == null) throw new StreamNotFound(stream);

        return result.Select(x => ToStreamEvent(x)).ToArray();
    }

    public async Task<AppendEventsResult> AppendEvents(
        StreamName                       stream,
        ExpectedStreamVersion            expectedVersion,
        IReadOnlyCollection<StreamEvent> events,
        CancellationToken                cancellationToken
    ) {
        var keys = new object[] {
            "append_events",
            3,
            stream.ToString(),
            expectedVersion.Value,
            DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)
        };

        var args = events
            .Where(x => x.Payload != null)
            .SelectMany(ConvertStreamEvent)
            .ToArray();

        var fCallParams = new object[keys.Length + args.Length];
        keys.CopyTo(fCallParams, 0);
        args.CopyTo(fCallParams, keys.Length);

        var database = _getDatabase();

        try {
            var response       = (RedisValue[]?)await database.ExecuteAsync("FCALL", fCallParams).NoContext();
            var streamPosition = (long)Ensure.NotNull(response?[0]);
            var globalPosition = Ensure.NotNull(response?[1]).ToString().AsSpan().ToULong();
            return new AppendEventsResult(globalPosition, streamPosition);
        }
        catch (Exception e) when (e.Message.Contains("WrongExpectedVersion")) {
            Log.UnableToAppendEvents(stream, e);
            throw new AppendToStreamException(stream, e);
        }

        object[] ConvertStreamEvent(StreamEvent evt) {
            var data = _serializer.SerializeEvent(evt.Payload!);
            var meta = _metaSerializer.Serialize(evt.Metadata);
            return new object[] { evt.Id.ToString(), data.EventType, AsString(data.Payload), AsString(meta) };
        }

        string AsString(ReadOnlySpan<byte> bytes)
            => Encoding.UTF8.GetString(bytes);
    }

    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) {
        var database = _getDatabase();
        var info     = await database.StreamInfoAsync(stream.ToString()).NoContext();

        return (info.Length > 0);
    }

    StreamEvent ToStreamEvent(StreamEntry evt) {
        var deserialized = _serializer.DeserializeEvent(
            Encoding.UTF8.GetBytes(evt["json_data"].ToString()),
            evt["message_type"].ToString(),
            ContentType
        );

        var meta = (string?)evt["json_metadata"] == null
            ? new Metadata()
            : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt["json_metadata"]!));

        return deserialized switch {
            SuccessfullyDeserialized success => AsStreamEvent(success.Payload),
            FailedToDeserialize failed => throw new SerializationException(
                $"Can't deserialize {evt["message_type"]}: {failed.Error}"
            ),
            _ => throw new Exception("Unknown deserialization result")
        };

        StreamEvent AsStreamEvent(object payload)
            => new(Guid.Parse(evt["message_id"].ToString()), payload, meta ?? new Metadata(), ContentType, evt.Id.ToLong());
    }
}
