// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using MongoDB.Bson.Serialization.Attributes;

namespace Eventuous.MongoDB.Snapshots;

/// <summary>
/// MongoDB document representation of a snapshot.
/// </summary>
record SnapshotDocument {
    [BsonId]
    public string   StreamName { get; init; } = null!;
    public long     Revision   { get; init; }
    public string   EventType  { get; init; } = null!;
    public string   JsonData   { get; init; } = null!;
    public DateTime Created    { get; init; }
}

