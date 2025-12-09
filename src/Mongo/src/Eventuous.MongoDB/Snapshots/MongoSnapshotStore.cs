// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.Serialization;
using System.Text;
using static Eventuous.DeserializationResult;

namespace Eventuous.MongoDB.Snapshots;

/// <summary>
/// MongoDB snapshot store implementation for storing snapshots separately from event streams.
/// </summary>
public class MongoSnapshotStore : ISnapshotStore {
    readonly IMongoCollection<SnapshotDocument> _collection;
    readonly IEventSerializer                   _serializer;
    const    string                             ContentType = "application/json";

    public MongoSnapshotStore(
            IMongoDatabase              database,
            MongoSnapshotStoreOptions?  options,
            IEventSerializer?           serializer = null
        ) {
        var mongoOptions = options ?? new MongoSnapshotStoreOptions();
        _collection = Ensure.NotNull(database).GetCollection<SnapshotDocument>(mongoOptions.CollectionName);
        _serializer = serializer ?? DefaultEventSerializer.Instance;
    }

    /// <inheritdoc />
    [RequiresDynamicCode("Only works with AOT when using DefaultStaticEventSerializer")]
    [RequiresUnreferencedCode("Only works with AOT when using DefaultStaticEventSerializer")]
    public async Task<Snapshot?> Read(StreamName streamName, CancellationToken cancellationToken = default) {
        var document = await _collection
            .Find(Builders<SnapshotDocument>.Filter.Eq(x => x.StreamName, streamName.ToString()))
            .SingleOrDefaultAsync(cancellationToken)
            .NoContext();

        if (document == null) {
            return null;
        }

        var deserialized = _serializer.DeserializeEvent(
            Encoding.UTF8.GetBytes(document.JsonData),
            document.EventType,
            ContentType
        );

        return deserialized switch {
            SuccessfullyDeserialized success => new Snapshot {
                Revision = document.Revision,
                Payload  = success.Payload
            },
            FailedToDeserialize failed => throw new SerializationException($"Can't deserialize snapshot {document.EventType}: {failed.Error}"),
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

        var document = new SnapshotDocument {
            StreamName = streamName.ToString(),
            Revision   = snapshot.Revision,
            EventType  = serialized.EventType,
            JsonData   = jsonData,
            Created    = DateTime.UtcNow
        };

        await _collection.ReplaceOneAsync(
                Builders<SnapshotDocument>.Filter.Eq(x => x.StreamName, streamName.ToString()),
                document,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken
            )
            .NoContext();
    }

    /// <inheritdoc />
    public async Task Delete(StreamName streamName, CancellationToken cancellationToken = default) {
        await _collection.DeleteOneAsync(
                Builders<SnapshotDocument>.Filter.Eq(x => x.StreamName, streamName.ToString()),
                cancellationToken
            )
            .NoContext();
    }
}

