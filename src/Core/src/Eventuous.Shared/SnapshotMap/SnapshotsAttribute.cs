// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[AttributeUsage(AttributeTargets.Class)]
public class SnapshotsAttribute(params Type[] snapshotTypes) : Attribute {
    public Type[] SnapshotTypes { get; } = snapshotTypes;

    /// <summary>
    /// Storage strategy for snapshot events
    /// </summary>
    public SnapshotStorageStrategy StorageStrategy { get; set; } = SnapshotStorageStrategy.SameStream;
}

[AttributeUsage(AttributeTargets.Class)]
public class SnapshotsAttribute<T> : SnapshotsAttribute {
    public SnapshotsAttribute(SnapshotStorageStrategy storageStrategy = SnapshotStorageStrategy.SameStream) : base(typeof(T)) {
        StorageStrategy = storageStrategy;
    }
}
