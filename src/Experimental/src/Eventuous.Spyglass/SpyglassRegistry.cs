// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Spyglass;

public delegate StreamName SpyglassGetStreamName(StreamNameMap? map, string entityId);

public delegate Task<SpyglassLoadResult?> SpyglassLoadDelegate(IEventStore eventStore, StreamName streamName, int version);

public record SpyglassAggregateInfo(
        string?                AggregateType,
        string                 StateType,
        string[]               Methods,
        string[]               Events,
        SpyglassGetStreamName  GetStreamName,
        SpyglassLoadDelegate   LoadDelegate
    ) {
    public Guid Id { get; init; }
}

public record SpyglassAggregateEntry(Guid Id, string? AggregateType, string StateType, string[] Methods, string[] Events);

public record SpyglassLoadResult(object State, SpyglassEventInfo[] Events);

public record SpyglassEventInfo(string EventType, object? Payload);

public static class SpyglassRegistry {
    // Module initializers from different assemblies can call Register concurrently when the assemblies
    // are loaded by parallel test fixtures (or any parallel host startup), so the backing store has to
    // be thread-safe. Reads also enumerate the snapshot, so we publish a fresh array on every write.
    static SpyglassAggregateInfo[] _aggregates = [];
    static readonly object         _lock       = new();

    public static void Register(SpyglassAggregateInfo info) {
        lock (_lock) {
            var entry = info with { Id = Guid.NewGuid() };
            var next  = new SpyglassAggregateInfo[_aggregates.Length + 1];
            Array.Copy(_aggregates, next, _aggregates.Length);
            next[^1]    = entry;
            _aggregates = next;
        }
    }

    public static SpyglassAggregateEntry[] GetAggregates()
        => _aggregates.Select(a => new SpyglassAggregateEntry(a.Id, a.AggregateType, a.StateType, a.Methods, a.Events)).ToArray();

    public static SpyglassAggregateInfo? FindById(Guid id)
        => Array.Find(_aggregates, x => x.Id == id);

    public static SpyglassAggregateInfo? FindByTypeName(string typeName) {
        var snapshot = _aggregates;

        return Array.Find(snapshot, x => x.AggregateType                == typeName)
            ?? Array.Find(snapshot, x => StripStateSuffix(x.StateType) == typeName);
    }

    static string StripStateSuffix(string s)
        => s.EndsWith("State") && s.Length > 5 ? s[..^5] : s;
}
