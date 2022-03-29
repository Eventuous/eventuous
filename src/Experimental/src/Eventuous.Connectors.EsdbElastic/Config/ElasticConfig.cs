using Eventuous.Connectors.EsdbElastic.Index;

namespace Eventuous.Connectors.EsdbElastic.Config;

public record ElasticConfig {
    public string?     ConnectionString { get; init; }
    public string?     ApiKey           { get; init; }
    public IndexConfig DataStream       { get; init; } = null!;
}
