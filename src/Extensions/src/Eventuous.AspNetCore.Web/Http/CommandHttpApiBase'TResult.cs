// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Http;

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
    /// <returns>A custom result class that inherits from <see cref="Result"/>.</returns>
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
    /// <returns>A custom result class that inherits from <see cref="Result"/>.</returns>
    /// <exception cref="InvalidOperationException">Throws if the command map hasn't been configured</exception>
    protected async Task<ActionResult<TResult>> Handle<TContract, TCommand>(TContract httpCommand, CancellationToken cancellationToken)
        where TContract : class where TCommand : class {
        if (commandMap == null) throw new InvalidOperationException("Command map is not configured");

        var command = commandMap.Convert<TContract, TCommand>(httpCommand);
        var result  = await service.Handle(command, cancellationToken);

        return AsActionResult<TAggregate>(result);
    }

    protected virtual ActionResult<TResult> AsActionResult<T>(Result result) where T : Aggregate {
        return result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException<T> => AsProblemResult(StatusCodes.Status409Conflict),
                AggregateNotFoundException<T>     => AsProblemResult(StatusCodes.Status404NotFound),
                DomainException                   => AsValidationProblemResult(StatusCodes.Status400BadRequest),
                _                                 => AsProblemResult(StatusCodes.Status500InternalServerError)
            }
            : new OkObjectResult(result);

        ActionResult AsProblemResult(int statusCode)
            => new ObjectResult(
                new ProblemDetails {
                    Status = statusCode,
                    Title = error.ErrorMessage,
                    Detail = error.Exception?.ToString(),
                    Type = error.Exception?.GetType().Name
                }
            ) {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = new MediaTypeCollection { ContentTypes.ProblemDetails },
            };

        ActionResult AsValidationProblemResult(int statusCode)
            => new ObjectResult(
                new ValidationProblemDetails(new Dictionary<string, string[]> { ["Domain"] = [error.ErrorMessage] }) {
                    Status = statusCode,
                    Title = error.ErrorMessage,
                    Detail = error.Exception?.ToString(),
                    Type = error.Exception?.GetType().Name
                }
            ) {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = new MediaTypeCollection { ContentTypes.ProblemDetails },
            };
    }
}
