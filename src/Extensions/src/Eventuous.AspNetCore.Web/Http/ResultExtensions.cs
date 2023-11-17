// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eventuous.AspNetCore.Web;

public static class ResultExtensions {
    public static IResult AsResult(this Result result) {
        return result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException => AsProblemDetails(Status409Conflict),
                AggregateNotFoundException     => AsProblemDetails(Status404NotFound),
                DomainException                => AsValidationProblemDetails(Status400BadRequest),
                _                              => AsProblemDetails(Status500InternalServerError)
            }
            : Results.Ok(result);

        IResult AsProblemDetails(int statusCode)
            => Results.Problem(
                new ProblemDetails {
                    Status = statusCode,
                    Title  = error.ErrorMessage,
                    Detail = error.Exception?.ToString(),
                    Type   = error.Exception?.GetType().Name
                }
            );

        IResult AsValidationProblemDetails(int statusCode)
            => Results.ValidationProblem(
                errors: error.AsErrors(),
                statusCode: statusCode,
                title: error.ErrorMessage,
                detail: error.Exception?.ToString(),
                type: error.Exception?.GetType().Name
            );
    }

    public static ActionResult AsActionResult(this Result result) {
        return result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException => AsProblemResult(Status409Conflict),
                AggregateNotFoundException     => AsProblemResult(Status404NotFound),
                DomainException                => AsValidationProblemResult(Status400BadRequest),
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

        ActionResult AsValidationProblemResult(int statusCode)
            => new ObjectResult(
                new ValidationProblemDetails(error.AsErrors()) {
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

    public static IDictionary<string, string[]> AsErrors(this ErrorResult error)
        => new Dictionary<string, string[]> { ["Domain"] = new[] { error.ErrorMessage } };
}
