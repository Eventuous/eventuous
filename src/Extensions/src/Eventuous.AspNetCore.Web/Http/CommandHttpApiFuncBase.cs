// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.AspNetCore.Web;

/// <summary>
/// Base class for exposing commands via Web API using a controller.
/// </summary>
/// <typeparam name="TState">State type</typeparam>
[PublicAPI]
public abstract class CommandHttpApiBaseFunc<TState> : ControllerBase where TState : State<TState>, new() {
    readonly IFuncCommandService<TState> _service;
    readonly MessageMap?                 _commandMap;

    protected CommandHttpApiBaseFunc(IFuncCommandService<TState> service, MessageMap? commandMap = null) {
        _service    = service;
        _commandMap = commandMap;
    }

    /// <summary>
    /// Call this method from your HTTP endpoints to handle commands and wrap the result properly.
    /// </summary>
    /// <param name="command">Command instance</param>
    /// <param name="cancellationToken">Request cancellation token</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns></returns>
    protected async Task<ActionResult<Result>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class {
        var result = await _service.Handle(command, cancellationToken);
        return AsActionResult<TState>(result);
    }

    /// <summary>
    /// Call this method from your HTTP endpoints to handle commands where there is a mapping between
    /// HTTP contract and the domain command, and wrap the result properly.
    /// </summary>
    /// <param name="command">HTTP command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TContract">HTTP command type</typeparam>
    /// <typeparam name="TCommand">Domain command type</typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Throws if the command map hasn't been configured</exception>
    protected async Task<ActionResult<Result>> Handle<TContract, TCommand>(TContract command, CancellationToken cancellationToken)
        where TContract : class where TCommand : class {
        if (_commandMap == null) throw new InvalidOperationException("Command map is not configured");

        var cmd    = _commandMap.Convert<TContract, TCommand>(command);
        var result = await _service.Handle(cmd, cancellationToken);
        return AsActionResult<TState>(result);
    }

    static ActionResult<Result> AsActionResult<T>(Result result) where T : State<T>
        => result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException => new ConflictObjectResult(error),
                AggregateNotFoundException     => new NotFoundObjectResult(error),
                _                              => new BadRequestObjectResult(error)
            }
            : new OkObjectResult(result);
}
