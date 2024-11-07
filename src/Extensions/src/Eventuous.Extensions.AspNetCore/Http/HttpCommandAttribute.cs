// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Extensions.AspNetCore;

/// <summary>
/// Use this attribute on individual command contracts.
/// It can be used in combination with <see cref="HttpCommandsAttribute"/>.
/// In that case, you won't need to specify the aggregate type.
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
    /// Aggregate type for the command will be used to resolve the command service
    /// </summary>
    public Type? StateType { get; set; }

    /// <summary>
    /// Authorization policy name
    /// </summary>
    public string? PolicyName { get; set; }
}

[AttributeUsage(AttributeTargets.Class)]
public class HttpCommandAttribute<TState> : HttpCommandAttribute where TState : State<TState> {
    public HttpCommandAttribute() => StateType = typeof(TState);
}

/// <summary>
/// Use this attribute on a static class that contains individual command contracts nested inside.
/// All commands nested in the class annotated with this attribute must operate on a single state type.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class HttpCommandsAttribute(Type stateType) : Attribute {
    public Type StateType { get; set; } = stateType;
}

/// <summary>
/// Use this attribute on a static class that contains individual command contracts nested inside.
/// All commands nested in the class annotated with this attribute must operate on a single state type.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class HttpCommandsAttribute<TState>() : HttpCommandsAttribute(typeof(TState)) where TState : State<TState>;

static class AttributeCheck {
    public static void EnsureCorrectParent<TCommand, T>(HttpCommandAttribute? attr) {
        if (attr != null && attr.GetType().IsGenericType) {
            var stateType = attr.GetType().GetGenericArguments()[0];

            if (stateType != typeof(T)) {
                throw new InvalidOperationException(
                    $"Command {typeof(TCommand).Name} is mapped to state {stateType.Name} but the route builder is for state {typeof(T).Name}"
                );
            }
        }
    }
}
