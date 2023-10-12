// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.AspNetCore.Web;

/// <summary>
/// Use this attribute on individual command contracts. It can be used in combination with
/// <see cref="AggregateCommandsAttribute"/>, in that case you won't need to specify the aggregate type.
/// When used without a nesting class, the aggregate type is mandatory. The Route property
/// is optional, if you omit it, we'll use the command class name as the route.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class HttpCommandAttribute : Attribute {
    /// <summary>
    /// HTTP POST route for the command
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// Aggregate type for the command, will be used to resolve the command service
    /// </summary>
    public Type? AggregateType { get; set; }

    /// <summary>
    /// The result type with as default <see cref="Result"/>.
    /// </summary>
    public Type ResultType { get; set; } = typeof(Result);

    /// <summary>
    /// Authorization policy name
    /// </summary>
    public string? PolicyName { get; set; }
}

[AttributeUsage(AttributeTargets.Class)]
public class HttpCommandAttribute<TAggregate> : HttpCommandAttribute where TAggregate : Aggregate {
    public HttpCommandAttribute() => AggregateType = typeof(TAggregate);
}

[AttributeUsage(AttributeTargets.Class)]
public class HttpCommandAttribute<TAggregate, TResult> : HttpCommandAttribute where TAggregate : Aggregate where TResult : Result {
    public HttpCommandAttribute() {
        AggregateType = typeof(TAggregate);
        ResultType    = typeof(TResult);
    }
}

[AttributeUsage(AttributeTargets.Class)]
public class AggregateCommandsAttribute(Type aggregateType) : Attribute {
    public Type  AggregateType { get; set; } = aggregateType;
    public Type? ResultType    { get; set; }
}

/// <summary>
/// Use this attribute on a static class that contains individual command contracts, so we know which aggregate
/// the commands in the nesting class contains. All commands nested in the class annotated with this attribute
/// must operate on a single aggregate type.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class AggregateCommandsAttribute<TAggregate>() : AggregateCommandsAttribute(typeof(TAggregate)) where TAggregate : Aggregate;

[AttributeUsage(AttributeTargets.Class)]
public class AggregateCommandsAttribute<TAggregate, TResult> : AggregateCommandsAttribute
    where TAggregate : Aggregate where TResult : Result {
    public AggregateCommandsAttribute() : base(typeof(TAggregate)) {
        ResultType = typeof(TResult);
    }
}

static class AttributeCheck {
    public static void EnsureCorrectAggregate<TCommand, T>(HttpCommandAttribute? attr) {
        if (attr != null && attr.GetType().IsGenericType) {
            var aggregateType = attr.GetType().GetGenericArguments()[0];

            if (aggregateType != typeof(T)) {
                throw new InvalidOperationException(
                    $"Command {typeof(TCommand).Name} is mapped to aggregate {aggregateType.Name} but the route builder is for aggregate {typeof(T).Name}"
                );
            }
        }
    }
}
