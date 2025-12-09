// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Eventuous.Redis.Snapshots;

public class RedisSnapshotStoreOptions {
    /// <summary>
    /// Redis key prefix for snapshots. Default is "snapshot".
    /// </summary>
    public string KeyPrefix { get; set; } = "snapshot";
}

