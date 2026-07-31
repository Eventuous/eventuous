// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Spyglass;

#if NET8_0
using Lock = object;
#endif

public delegate StreamName SpyglassGetStreamName(StreamNameMap? map, string entityId);

public delegate Task<SpyglassLoadResult?> SpyglassLoadDelegate(IEventStore eventStore, StreamName streamName, int version);

public record SpyglassAggregateInfo(
        string?               AggregateType,
        string                StateType,
        string[]              Methods,
        string[]              Events,
        SpyglassGetStreamName GetStreamName,
        SpyglassLoadDelegate  LoadDelegate
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
    static          SpyglassAggregateInfo[] aggregates = [];
    static readonly Lock                    Lock       = new();

    public static void Register(SpyglassAggregateInfo info) {
        lock (Lock) {
            var entry = info with { Id = Guid.NewGuid() };
            var next  = new SpyglassAggregateInfo[aggregates.Length + 1];
            Array.Copy(aggregates, next, aggregates.Length);
            next[^1]   = entry;
            aggregates = next;
        }
    }

    public static SpyglassAggregateEntry[] GetAggregates()
        => [.. aggregates.Select(a => new SpyglassAggregateEntry(a.Id, a.AggregateType, a.StateType, a.Methods, a.Events))];

    public static SpyglassAggregateInfo? FindById(Guid id)
        => Array.Find(aggregates, x => x.Id == id);
}
