// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
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

    /// <summary>
    /// Reads events from a stream. Positions are inclusive of the start position.
    /// Streams containing entries written by pre-0.16 versions with auto-generated IDs whose
    /// sequence number exceeds 9 are not readable from a non-zero position: positions for such
    /// entries don't round-trip through the position encoding, so resumed reads are rejected with
    /// <see cref="NotSupportedException"/> instead of risking silently skipped events. Read such
    /// streams from the start, which fails loudly on the first unrepresentable entry, and migrate them.
    /// </summary>
    public async IAsyncEnumerable<StreamEvent> ReadEvents(StreamName stream, StreamReadPosition start, int count, [EnumeratorCancellation] CancellationToken cancellationToken) {
        StreamEvent[] events;
        var           database = _getDatabase();

        try {
            // A resumed position is only unambiguous when every entry ID in the stream round-trips
            // through the position encoding (sequence numbers 0-9). Entries the encoding can't
            // represent can hide below the decoded start position while falling inside the
            // requested range, so reads from a non-zero position are conservatively rejected for
            // streams holding any such entry.
            if (start.Value >= 10) {
                await EnsureStreamPositionsRoundTrip(database, stream, cancellationToken).NoContext();
            }

            // Range read is inclusive of the start position, matching the IEventReader contract
            // and the paged read extensions, which advance pages from the last revision + 1
            var result = await database.StreamRangeAsync(stream.ToString(), start.Value.ToRedisValue(), count: count).NoContext();

            if (result == null! || result.Length == 0) {
                // An empty result can also mean the read window is past the stream end
                if (!await database.KeyExistsAsync(stream.ToString()).NoContext()) {
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

    const int ValidationPageSize = 1000;

    readonly ConcurrentDictionary<string, (RedisValue First, RedisValue Last)> _validatedStreams = new();

    // Validates that every entry ID in the stream round-trips through the position encoding.
    // The verdict is cached per stream, anchored on the first entry ID: Redis only accepts
    // appends with increasing entry IDs, so a validated range can't gain new entries, and a
    // changed first entry means the stream was recreated. Only entries appended after the last
    // validated one are scanned on subsequent reads, one bounded page at a time.
    async ValueTask EnsureStreamPositionsRoundTrip(IDatabase database, string stream, CancellationToken cancellationToken) {
        var head = await database.StreamRangeAsync(stream, "-", "+", count: 1).NoContext();

        // A missing stream holds nothing to validate, and no verdict is recorded for the name
        if (head.Length == 0) return;

        var first = head[0].Id;

        RedisValue from;
        RedisValue last;

        if (_validatedStreams.TryGetValue(stream, out var validated) && validated.First == first) {
            from = $"({validated.Last}";
            last = validated.Last;
        } else {
            from = "-";
            last = first;
        }

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await database.StreamRangeAsync(stream, from, "+", count: ValidationPageSize).NoContext();

            if (batch.Length == 0) break;

            foreach (var entry in batch) {
                if (EntrySequence(entry.Id) > 9) {
                    throw new NotSupportedException(
                        $"Stream {stream} can't be read from a non-zero position: it contains entry ID {entry.Id}, which the position encoding can't represent (only ID sequence numbers 0-9 are supported). " +
                        "Entries with higher sequence numbers were written with auto-generated IDs by an older version of the store. Read the stream from the start and migrate it."
                    );
                }
            }

            last = batch[^1].Id;
            from = $"({last}";

            if (batch.Length < ValidationPageSize) break;
        }

        _validatedStreams[stream] = (first, last);
    }

    static long EntrySequence(RedisValue id) {
        var value = Ensure.NotNull<string>(id);

        return long.Parse(value.AsSpan(value.IndexOf('-') + 1));
    }

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
            => new(Guid.Parse(evt[MessageId].ToString()), payload, meta ?? new Metadata(), ContentType, evt.Id.ToRevision(), DateTime.Parse(evt[Created]!, CultureInfo.InvariantCulture));
    }
}
