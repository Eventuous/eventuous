// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Eventuous.Connectors.EsdbElastic.Index;

public record IndexConfig(DataStreamTemplateConfig TemplateConfig, LifecycleConfig LifecycleConfig);

public record DataStreamTemplateConfig(
    string TemplateName,
    string IndexPattern,
    int    NumberOfShards   = 1,
    int    NumberOrReplicas = 1
);

public record LifecycleConfig(string PolicyName, TierDefinition[] Tiers);
    
public record TierDefinition(string Tier) {
    public string?     MinAge     { get; init; }
    public Rollover?   Rollover   { get; init; }
    public int?        Priority   { get; init; }
    public ForceMerge? ForceMerge { get; init; }
    public bool        ReadOnly   { get; init; }
    public bool        Delete     { get; init; }
}

public record Rollover(string? MaxAge, long? MaxDocs, string? MaxSize);

public record ForceMerge(int MaxNumSegments);