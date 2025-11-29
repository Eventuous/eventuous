// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class SnapshotTypeMap {

    static readonly Dictionary<Type, HashSet<Type>> StateToSnapshots = [];

    public static void Register(Type stateType, Type eventType) {
        if (StateToSnapshots.TryGetValue(stateType, out var value)) {
            value.Add(eventType);
        } else {
            StateToSnapshots[stateType] = [eventType];
        }
    }

    public static HashSet<Type> GetSnapshotTypes<TState>() => StateToSnapshots.GetValueOrDefault(typeof(TState), []);
}

[AttributeUsage(AttributeTargets.Class)]
public class SnapshotsAttribute(params Type[] snapshotTypes) : Attribute {
    public Type[] SnapshotTypes { get; } = snapshotTypes;
}