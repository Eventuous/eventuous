// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using static Eventuous.DeserializationResult;
using static Eventuous.Diagnostics.PersistenceEventSource;
using static Eventuous.Redis.EventuousRedisKeys;

namespace Eventuous.Redis;

using Tools;

public delegate IDatabase GetRedisDatabase();

public record RedisStoreOptions;

public class RedisStore : IEventReader, IEventWriter {
    readonly GetRedisDatabase    _getDatabase;
    readonly IEventSerializer    _serializer;
    readonly IMetadataSerializer _metaSerializer;

    public RedisStore(
            GetRedisDatabase getDatabase,
            // ReSharper disable once UnusedParameter.Local
            RedisStoreOptions    options,
            IEventSerializer?    serializer     = null,
            IMetadataSerializer? metaSerializer = null
        ) {
        _serializer     = serializer     ?? EventSerializer.Default;
        _metaSerializer = metaSerializer ?? DefaultMetadataSerializer.Instance;
        _getDatabase    = Ensure.NotNull(getDatabase, "Connection factory");
    }

    const string ContentType = "application/json";

    public async IAsyncEnumerable<StreamEvent> ReadEvents(StreamName stream, StreamReadPosition start, int count, [EnumeratorCancellation] CancellationToken cancellationToken) {
        StreamEvent[] events;

        try {
            var result = await _getDatabase().StreamReadAsync(stream.ToString(), start.Value.ToRedisValue(), count).NoContext();

            if (result == null! || result.Length == 0) {
                // An empty result can also mean the read window is past the stream end
                if (!await _getDatabase().KeyExistsAsync(stream.ToString()).NoContext()) {
                    throw new StreamNotFound(stream);
                }

                events = [];
            } else {
                events = [.. result.Select(x => ToStreamEvent(x, _serializer, _metaSerializer))];
            }
        } catch (InvalidOperationException e) when (e.Message.Contains("Reading is not allowed after reader was completed") ||
                                                    cancellationToken.IsCancellationRequested) {
            throw new OperationCanceledException("Redis read operation terminated", e, cancellationToken);
        }

        foreach (var evt in events) yield return evt;
    }

    public IAsyncEnumerable<StreamEvent> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public async Task<AppendEventsResult> AppendEvents(
            StreamName                          stream,
            ExpectedStreamVersion               expectedVersion,
            IReadOnlyCollection<NewStreamEvent> events,
            CancellationToken                   cancellationToken
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
            var response             = (RedisValue[]?)await database.ExecuteAsync("FCALL", fCallParams).NoContext();
            var streamPosition       = (long)Ensure.NotNull(response?[0]);
            var globalPositionString = Ensure.NotNull(response?[1]).ToString();
            var globalPosition       = globalPositionString.AsSpan().ToULong();

            return new(globalPosition, streamPosition);
        } catch (Exception e) when (e.Message.Contains("WrongExpectedVersion")) {
            Log.UnableToAppendEvents(stream, e);

            throw new AppendToStreamException(stream, e);
        }

        object[] ConvertStreamEvent(NewStreamEvent evt) {
            var data = _serializer.SerializeEvent(evt.Payload!);
            var meta = _metaSerializer.Serialize(evt.Metadata);

            return [evt.Id.ToString(), data.EventType, AsString(data.Payload), AsString(meta)];
        }

        string AsString(ReadOnlySpan<byte> bytes)
            => Encoding.UTF8.GetString(bytes);
    }

    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) {
        var database = _getDatabase();
        var info     = await database.StreamInfoAsync(stream.ToString()).NoContext();

        return (info.Length > 0);
    }

    static StreamEvent ToStreamEvent(StreamEntry evt, IEventSerializer serializer, IMetadataSerializer metaSerializer) {
        var deserialized = serializer.DeserializeEvent(
            Encoding.UTF8.GetBytes(evt[JsonData].ToString()),
            evt["message_type"].ToString(),
            ContentType
        );

        var meta = (string?)evt[JsonMetadata] == null ? new() : metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt[JsonMetadata]!));

        return deserialized switch {
            SuccessfullyDeserialized success => AsStreamEvent(success.Payload),
            FailedToDeserialize failed => throw new SerializationException(
                $"Can't deserialize {evt[MessageType]}: {failed.Error}"
            ),
            _ => throw new("Unknown deserialization result")
        };

        StreamEvent AsStreamEvent(object payload)
            => new(Guid.Parse(evt[MessageId].ToString()), payload, meta ?? new Metadata(), ContentType, evt.Id.ToLong(), DateTime.Parse(evt[Created]!, CultureInfo.InvariantCulture));
    }
}
