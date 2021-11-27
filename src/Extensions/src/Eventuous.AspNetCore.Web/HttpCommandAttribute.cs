namespace Eventuous.AspNetCore.Web;

[PublicAPI]
[AttributeUsage(AttributeTargets.Class)]
public class HttpCommandAttribute : Attribute {
    public string? Route         { get; set; }
    public Type?   AggregateType { get; set; }
}

[PublicAPI]
[AttributeUsage(AttributeTargets.Class)]
public class AggregateCommands : Attribute {
    public AggregateCommands(Type aggregateType) => AggregateType = aggregateType;

    public Type AggregateType { get; }
}
