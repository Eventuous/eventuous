// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eventuous.AspNetCore.Web;

public static class ResultExtensions {
    public static IResult AsResult<TResult>(this Result result) where TResult : Result {
        return result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException => AsProblemDetails(Status409Conflict, error),
                AggregateNotFoundException     => AsProblemDetails(Status404NotFound, error),
                DomainException                => AsProblemDetails(Status400BadRequest, error),
                _                              => AsProblemDetails(Status500InternalServerError, error)
            }
            : Results.Ok(result);
    }

    static IResult AsProblemDetails(int statusCode, ErrorResult error)
        => Results.Problem(
            new ProblemDetails {
                Status = statusCode,
                Title  = error.ErrorMessage,
                Detail = error.Exception?.ToString(),
                Type   = error.Exception?.GetType().Name
            }
        );

    public static ActionResult<Result<T>> AsActionResult<T>(this Result result) where T : State<T>, new() {
        return result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException => AsProblemResult(Status409Conflict),
                AggregateNotFoundException     => AsProblemResult(Status404NotFound),
                DomainException                => AsProblemResult(Status400BadRequest),
                _                              => AsProblemResult(Status500InternalServerError)
            }
            : new OkObjectResult(result);

        ActionResult AsProblemResult(int statusCode)
            => new ObjectResult(
                new ProblemDetails {
                    Status = statusCode,
                    Title  = error.ErrorMessage,
                    Detail = error.Exception?.ToString(),
                    Type   = error.Exception?.GetType().Name
                }
            ) {
                StatusCode   = Status400BadRequest,
                ContentTypes = new MediaTypeCollection { ContentTypes.ProblemDetails },
            };
    }
}
