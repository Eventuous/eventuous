// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Eventuous.MongoDB.Snapshots;

/// <summary>
/// MongoDB snapshot store options.
/// </summary>
[PublicAPI]
public class MongoSnapshotStoreOptions {
    /// <summary>
    /// MongoDB connection string.
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Database name. Default is "eventuous".
    /// </summary>
    public string DatabaseName { get; set; } = "eventuous";

    /// <summary>
    /// Collection name for snapshots. Default is "snapshots".
    /// </summary>
    public string CollectionName { get; set; } = "snapshots";

    /// <summary>
    /// Set to true to initialize indexes on startup. Default is false.
    /// </summary>
    public bool InitializeIndexes { get; set; }
}
