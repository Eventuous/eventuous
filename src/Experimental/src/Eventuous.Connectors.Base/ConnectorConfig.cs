namespace Eventuous.Connectors.Base;

public record ConnectorConfig<TSource, TTarget> where TSource : class where TTarget : class {
    public string  ConnectorId { get; init; } = "default";
    public TSource Source      { get; init; } = null!;
    public TTarget Target      { get; init; } = null!;
}
