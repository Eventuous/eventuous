namespace Eventuous.AspNetCore.Web;

/// <summary>
/// Use this attribute on individual command contracts. It can be used in combination with
/// <see cref="AggregateCommands"/>, in that case you won't need to specify the aggregate type.
/// When used without a nesting class, the aggregate type is mandatory. The Route property
/// is optional, if you omit it, we'll use the command class name as the route.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class)]
public class HttpCommandAttribute : Attribute {
    public string? Route         { get; set; }
    public Type?   AggregateType { get; set; }
}

/// <summary>
/// Use this attribute on a static class that contains individual command contracts, so we know which aggregate
/// the commands in the nesting class contains. All commands nested in the class annotated with this attribute
/// must operate on a single aggregate type.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class)]
public class AggregateCommands : Attribute {
    public AggregateCommands(Type aggregateType) => AggregateType = aggregateType;

    public Type AggregateType { get; }
}
