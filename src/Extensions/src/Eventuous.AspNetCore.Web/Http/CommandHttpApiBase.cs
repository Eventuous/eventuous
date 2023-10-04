// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.AspNetCore.Web;

/// <summary>
/// Base class for exposing commands via Web API using a controller.
/// </summary>
/// <typeparam name="TAggregate">Aggregate type</typeparam>
/// <typeparam name="TResult">Result type</typeparam>
[PublicAPI]
public abstract class CommandHttpApiBase<TAggregate, TResult>(ICommandService<TAggregate> service, MessageMap? commandMap = null) : ControllerBase
    where TAggregate : Aggregate
    where TResult : Result {

    /// <summary>
    /// Call this method from your HTTP endpoints to handle commands and wrap the result properly.
    /// </summary>
    /// <param name="command">Command instance</param>
    /// <param name="cancellationToken">Request cancellation token</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns>A custom result class that inherites from <see cref="Result"/>.</returns>
    protected async Task<ActionResult<TResult>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class {
        var result = await service.Handle(command, cancellationToken);

        return AsActionResult<TAggregate>(result);
    }

    /// <summary>
    /// Call this method from your HTTP endpoints to handle commands where there is a mapping between
    /// HTTP contract and the domain command, and wrap the result properly.
    /// </summary>
    /// <param name="httpCommand">HTTP command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TContract">HTTP command type</typeparam>
    /// <typeparam name="TCommand">Domain command type</typeparam>
    /// <returns>A custom result class that inherites from <see cref="Result"/>.</returns>
    /// <exception cref="InvalidOperationException">Throws if the command map hasn't been configured</exception>
    protected async Task<ActionResult<TResult>> Handle<TContract, TCommand>(TContract httpCommand, CancellationToken cancellationToken)
        where TContract : class where TCommand : class {
        if (commandMap == null) throw new InvalidOperationException("Command map is not configured");

        var command    = commandMap.Convert<TContract, TCommand>(httpCommand);
        var result = await service.Handle(command, cancellationToken);

        return AsActionResult<TAggregate>(result);
    }

    static ActionResult<TResult> AsActionResult<T>(Result result) where T : Aggregate
        => result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException<T> => new ConflictObjectResult(error),
                AggregateNotFoundException<T>     => new NotFoundObjectResult(error),
                _                                 => new BadRequestObjectResult(error)
            }
            : new OkObjectResult(result);
}
