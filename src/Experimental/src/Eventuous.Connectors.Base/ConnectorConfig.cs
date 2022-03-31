namespace Eventuous.Connectors.Base;

public record ConnectorConfig<TSource, TTarget> where TSource : class where TTarget : class {
    public ConnectorSettings Connector { get; init; } = new();
    public TSource           Source    { get; init; } = null!;
    public TTarget           Target    { get; init; } = null!;
}

public record ConnectorSettings {
    public string            ConnectorId { get; init; } = "default";
    public DiagnosticsConfig Diagnostics { get; init; } = new();
}

public record DiagnosticsConfig {
    public bool Enabled    { get; init; } = true;
    public bool Trace      { get; init; } = true;
    public bool Metrics    { get; init; } = true;
    public bool Prometheus { get; init; } = true;
}
