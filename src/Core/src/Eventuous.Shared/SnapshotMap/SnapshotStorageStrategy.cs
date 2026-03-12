// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Strategy for storing snapshot events
/// </summary>
public enum SnapshotStorageStrategy {
    /// <summary>
    /// Store snapshots in the same stream as other aggregate events
    /// </summary>
    SameStream,

    /// <summary>
    /// Store snapshots in a separate stream
    /// </summary>
    SeparateStream,

    /// <summary>
    /// Store snapshots in a separate storage (e.g., PostgreSQL, Redis, etc.)
    /// </summary>
    SeparateStore
}
