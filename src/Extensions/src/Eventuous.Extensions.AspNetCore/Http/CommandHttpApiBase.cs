// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Http;

namespace Eventuous.Extensions.AspNetCore;

/// <summary>
/// Base class for exposing commands via Web API using a controller that returns the default result.
/// </summary>
/// <typeparam name="TState">State type</typeparam>
[PublicAPI]
public abstract class CommandHttpApiBase<TState>(ICommandService<TState> service, CommandMap<HttpContext>? commandMap = null)
    : CommandHttpApiBase<TState, Result<TState>.Ok>(service, commandMap) where TState : State<TState>, new();

/// <summary>
/// Base class for exposing commands via Web API using a controller returning a custom result type.
/// </summary>
/// <param name="service">Command service</param>
/// <param name="commandMap">Optional: Map between external and internal commands</param>
/// <typeparam name="TState">State type</typeparam>
/// <typeparam name="TResult">Custom result type</typeparam>
[PublicAPI]
public abstract class CommandHttpApiBase<TState, TResult>(ICommandService<TState> service, CommandMap<HttpContext>? commandMap = null) : ControllerBase
    where TState : State<TState>, new() {
    /// <summary>
    /// Call this method from your HTTP endpoints to handle commands and wrap the result properly.
    /// </summary>
    /// <param name="command">Command instance</param>
    /// <param name="cancellationToken">Request cancellation token</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns>A custom result class that inherits from <see cref="Result{TState}"/>.</returns>
    protected virtual async Task<ActionResult<TResult>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class {
        var result = await service.Handle(command, cancellationToken);

        return AsActionResult(result);
    }

    /// <summary>
    /// Call this method from your HTTP endpoints to handle commands where there is a mapping between
    /// HTTP contract and the domain command, and wrap the result properly.
    /// </summary>
    /// <param name="httpCommand">HTTP command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TContract">HTTP command type</typeparam>
    /// <typeparam name="TCommand">Domain command type</typeparam>
    /// <returns>A custom result class that inherits from <see cref="Result{TState}"/>.</returns>
    /// <exception cref="InvalidOperationException">Throws if the command map hasn't been configured</exception>
    protected virtual async Task<ActionResult<TResult>> Handle<TContract, TCommand>(TContract httpCommand, CancellationToken cancellationToken)
        where TContract : class where TCommand : class {
        if (commandMap == null) throw new InvalidOperationException("Command map is not configured");

        var command = commandMap.Convert<TContract, TCommand>(httpCommand, HttpContext);
        var result  = await service.Handle(command, cancellationToken);

        return AsActionResult(result);
    }

    /// <summary>
    /// Function to convert the default result to a custom result
    /// </summary>
    /// <param name="result">Command execution result</param>
    /// <returns>ActionResult with custom payload</returns>
    protected virtual ActionResult AsActionResult(Result<TState> result) => result.AsActionResult();
}
