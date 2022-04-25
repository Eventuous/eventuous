namespace Eventuous.Connectors.Base;

public record ConnectorConfig<TSource, TTarget> where TSource : class where TTarget : class {
    public ConnectorSettings Connector { get; init; } = new();
    public TSource           Source    { get; init; } = null!;
    public TTarget           Target    { get; init; } = null!;
}

public record ConnectorSettings {
    public string            ConnectorId { get; init; } = "default";
    public string            ServiceName { get; init; } = "eventuous-connector";
    public DiagnosticsConfig Diagnostics { get; init; } = new();
}

public record DiagnosticsConfig {
    public bool           Enabled                 { get; init; } = true;
    public TracingConfig? Tracing                 { get; init; }
    public MetricsConfig? Metrics                 { get; init; }
    public double         TraceSamplerProbability { get; init; } = 0;
}

public record MetricsConfig {
    public bool      Enabled   { get; init; } = true;
    public string[]? Exporters { get; init; }
}

public record TracingConfig {
    public bool      Enabled   { get; init; } = true;
    public string[]? Exporters { get; init; }
}
