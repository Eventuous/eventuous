// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class SnapshotTypeMap {

    static readonly Dictionary<Type, HashSet<Type>> StateToSnapshots = [];
    static readonly Dictionary<Type, SnapshotStorageStrategy> StateToStorageStrategy = [];

    public static void Register(Type stateType, Type eventType, SnapshotStorageStrategy storageStrategy = SnapshotStorageStrategy.SameStream) {
        if (StateToSnapshots.TryGetValue(stateType, out var value)) {
            value.Add(eventType);
        } else {
            StateToSnapshots[stateType] = [eventType];
            // Store strategy for the state type when first registering snapshots for this state
            StateToStorageStrategy[stateType] = storageStrategy;
        }
    }

    public static HashSet<Type> GetSnapshotTypes<TState>() => StateToSnapshots.GetValueOrDefault(typeof(TState), []);

    /// <summary>
    /// Gets the storage strategy for the specified state type
    /// </summary>
    /// <typeparam name="TState">The state type</typeparam>
    /// <returns>The storage strategy, or <see cref="SnapshotStorageStrategy.SameStream"/> if not specified</returns>
    public static SnapshotStorageStrategy GetStorageStrategy<TState>() => GetStorageStrategy(typeof(TState));

    /// <summary>
    /// Gets the storage strategy for the specified state type
    /// </summary>
    /// <param name="stateType">The state type</param>
    /// <returns>The storage strategy, or <see cref="SnapshotStorageStrategy.SameStream"/> if not specified</returns>
    public static SnapshotStorageStrategy GetStorageStrategy(Type stateType) => StateToStorageStrategy.GetValueOrDefault(stateType, SnapshotStorageStrategy.SameStream);
}
