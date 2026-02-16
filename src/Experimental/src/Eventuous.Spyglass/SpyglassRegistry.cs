// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Spyglass;

public delegate Task<SpyglassLoadResult> SpyglassLoadDelegate(IEventStore eventStore, string streamName, int version);

[PublicAPI]
public record SpyglassAggregateInfo(
        string?              AggregateType,
        string               StateType,
        string[]             Methods,
        string[]             Events,
        SpyglassLoadDelegate LoadDelegate
    );

[PublicAPI]
public record SpyglassLoadResult(object State, SpyglassEventInfo[] Events);

[PublicAPI]
public record SpyglassEventInfo(string EventType, object? Payload);

[PublicAPI]
public static class SpyglassRegistry {
    static readonly List<SpyglassAggregateInfo> Aggregates = [];

    public static void Register(SpyglassAggregateInfo info) => Aggregates.Add(info);

    public static SpyglassAggregateInfo[] GetAggregates() => [.. Aggregates];

    public static SpyglassAggregateInfo? FindByTypeName(string typeName)
        => Aggregates.FirstOrDefault(x => x.AggregateType                == typeName)
         ?? Aggregates.FirstOrDefault(x => StripStateSuffix(x.StateType) == typeName);

    static string StripStateSuffix(string s)
        => s.EndsWith("State") && s.Length > 5 ? s[..^5] : s;
}
