// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Eventuous.ElasticSearch.Index;

public record IndexConfig {
    public string?                   IndexName { get; init; }
    public DataStreamTemplateConfig? Template  { get; init; }
    public LifecycleConfig?          Lifecycle { get; init; }
}

public record DataStreamTemplateConfig {
    public string? TemplateName     { get; init; }
    public int     NumberOfShards   { get; init; } = 1;
    public int     NumberOrReplicas { get; init; } = 1;
}

public record LifecycleConfig {
    public string?           PolicyName { get; init; }
    public TierDefinition[]? Tiers      { get; init; }
}

public record TierDefinition {
    public string?     Tier       { get; init; }
    public string?     MinAge     { get; init; }
    public Rollover?   Rollover   { get; init; }
    public int?        Priority   { get; init; }
    public ForceMerge? ForceMerge { get; init; }
    public bool        ReadOnly   { get; init; }
    public bool        Delete     { get; init; }
}

public record Rollover {
    public string? MaxAge  { get; init; }
    public long?   MaxDocs { get; init; }
    public string? MaxSize { get; init; }

    public void Deconstruct(out string? maxAge, out long? maxDocs, out string? maxSize) {
        maxAge  = MaxAge;
        maxDocs = MaxDocs;
        maxSize = MaxSize;
    }
}

public record ForceMerge {
    public int MaxNumSegments { get; init; }
}
