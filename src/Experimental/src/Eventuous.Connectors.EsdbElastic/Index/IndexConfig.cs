// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Eventuous.Connectors.EsdbElastic.Index;

public record IndexConfig {
    public DataStreamTemplateConfig? Template  { get; init; }
    public LifecycleConfig?          Lifecycle { get; init; }

    public void Deconstruct(out DataStreamTemplateConfig? templateConfig, out LifecycleConfig? lifecycleConfig) {
        templateConfig  = Template;
        lifecycleConfig = Lifecycle;
    }
}

public record DataStreamTemplateConfig {
    public string? TemplateName     { get; init; }
    public string? IndexPattern     { get; init; }
    public int     NumberOfShards   { get; init; }
    public int     NumberOrReplicas { get; init; }
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
