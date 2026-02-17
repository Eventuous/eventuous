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
    static readonly List<SpyglassAggregateInfo> Aggregates = [];

    public static void Register(SpyglassAggregateInfo info)
        => Aggregates.Add(info with { Id = Guid.NewGuid() });

    public static SpyglassAggregateEntry[] GetAggregates()
        => Aggregates.Select(a => new SpyglassAggregateEntry(a.Id, a.AggregateType, a.StateType, a.Methods, a.Events)).ToArray();

    public static SpyglassAggregateInfo? FindById(Guid id)
        => Aggregates.FirstOrDefault(x => x.Id == id);

    public static SpyglassAggregateInfo? FindByTypeName(string typeName)
        => Aggregates.FirstOrDefault(x => x.AggregateType                == typeName)
         ?? Aggregates.FirstOrDefault(x => StripStateSuffix(x.StateType) == typeName);

    static string StripStateSuffix(string s)
        => s.EndsWith("State") && s.Length > 5 ? s[..^5] : s;
}
