namespace Eventuous.AspNetCore.Web;

/// <summary>
/// Use this attribute on individual command contracts. It can be used in combination with
/// <see cref="AggregateCommandsAttribute"/>, in that case you won't need to specify the aggregate type.
/// When used without a nesting class, the aggregate type is mandatory. The Route property
/// is optional, if you omit it, we'll use the command class name as the route.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class)]
public class HttpCommandAttribute : Attribute {
    public string? Route         { get; set; }
    public Type?   AggregateType { get; set; }
}

public class HttpCommandAttribute<T> : HttpCommandAttribute where T : Aggregate {
    public HttpCommandAttribute() {
        AggregateType = typeof(T);
    }
}

[PublicAPI]
[AttributeUsage(AttributeTargets.Class)]
public class AggregateCommandsAttribute : Attribute {
    public Type AggregateType { get; set; }

    public AggregateCommandsAttribute(Type aggregateType) {
        AggregateType = aggregateType;
    }
}

/// <summary>
/// Use this attribute on a static class that contains individual command contracts, so we know which aggregate
/// the commands in the nesting class contains. All commands nested in the class annotated with this attribute
/// must operate on a single aggregate type.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class)]
public class AggregateCommandsAttribute<T>  : AggregateCommandsAttribute where T : Aggregate {
    public AggregateCommandsAttribute() : base(typeof(T)) { }
}

public static class AttributeCheck {
    public static void EnsureCorrectAggregate<TCommand, T>(HttpCommandAttribute? attr) {
        if (attr != null && attr.GetType().IsGenericType) {
            var aggregateType = attr.GetType().GetGenericArguments()[0];
            if (aggregateType != typeof(T)) {
                throw new InvalidOperationException(
                    $"Command {typeof(TCommand).Name} is mapped to aggregate {aggregateType.Name} but " +
                    $"the route builder is for aggregate {typeof(T).Name}"
                );
            }
        }
    }
}