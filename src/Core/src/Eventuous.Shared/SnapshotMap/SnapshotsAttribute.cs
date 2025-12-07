// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[AttributeUsage(AttributeTargets.Class)]
public class SnapshotsAttribute : Attribute {
    public Type[] SnapshotTypes { get; }

    /// <summary>
    /// Storage strategy for snapshot events
    /// </summary>
    public SnapshotStorageStrategy StorageStrategy { get; set; } = SnapshotStorageStrategy.SameStream;

    public SnapshotsAttribute(params Type[] snapshotTypes) {
        SnapshotTypes = snapshotTypes;
    }
}