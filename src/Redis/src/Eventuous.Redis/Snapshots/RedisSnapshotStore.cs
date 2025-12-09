// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.Serialization;
using System.Text;
using Eventuous;
using static Eventuous.DeserializationResult;
using static Eventuous.Redis.EventuousRedisKeys;

namespace Eventuous.Redis.Snapshots;

using Tools;

/// <summary>
/// Redis snapshot store implementation for storing snapshots separately from event streams.
/// </summary>
public class RedisSnapshotStore : ISnapshotStore {
    readonly GetRedisDatabase           _getDatabase;
    readonly RedisSnapshotStoreOptions  _options;
    readonly IEventSerializer           _serializer;
    const    string                     ContentType = "application/json";

    public RedisSnapshotStore(
            GetRedisDatabase              getDatabase,
            RedisSnapshotStoreOptions?    options = null,
            IEventSerializer?             serializer = null
        ) {
        _getDatabase = Ensure.NotNull(getDatabase, "Connection factory");
        _options     = options ?? new RedisSnapshotStoreOptions();
        _serializer  = serializer ?? DefaultEventSerializer.Instance;
    }

    string GetSnapshotKey(StreamName streamName) => $"{_options.KeyPrefix}:{streamName}";

    /// <inheritdoc />
    [RequiresDynamicCode("Only works with AOT when using DefaultStaticEventSerializer")]
    [RequiresUnreferencedCode("Only works with AOT when using DefaultStaticEventSerializer")]
    public async Task<Snapshot?> Read(StreamName streamName, CancellationToken cancellationToken = default) {
        var database = _getDatabase();
        var key      = GetSnapshotKey(streamName);
        
        var revisionValue  = await database.HashGetAsync(key, Revision).NoContext();
        var eventTypeValue = await database.HashGetAsync(key, EventType).NoContext();
        var jsonDataValue  = await database.HashGetAsync(key, JsonData).NoContext();

        if (revisionValue.IsNull || eventTypeValue.IsNull || jsonDataValue.IsNull) {
            return null;
        }

        var revision   = revisionValue.ToString();
        var eventType  = eventTypeValue.ToString();
        var jsonData   = jsonDataValue.ToString();

        if (string.IsNullOrEmpty(revision) || string.IsNullOrEmpty(eventType) || string.IsNullOrEmpty(jsonData)) {
            return null;
        }

        var deserialized = _serializer.DeserializeEvent(
            Encoding.UTF8.GetBytes(jsonData),
            eventType,
            ContentType
        );

        return deserialized switch {
            SuccessfullyDeserialized success => new Snapshot {
                Revision = long.Parse(revision),
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
        var key        = GetSnapshotKey(streamName);
        var database   = _getDatabase();

        var hashFields = new HashEntry[] {
            new(Revision, snapshot.Revision.ToString()),
            new(EventType, serialized.EventType),
            new(JsonData, jsonData),
            new(Created, DateTime.UtcNow.ToString("O"))
        };

        await database.HashSetAsync(key, hashFields).NoContext();
    }

    /// <inheritdoc />
    public async Task Delete(StreamName streamName, CancellationToken cancellationToken = default) {
        var database = _getDatabase();
        var key      = GetSnapshotKey(streamName);

        await database.KeyDeleteAsync(key).NoContext();
    }
}

