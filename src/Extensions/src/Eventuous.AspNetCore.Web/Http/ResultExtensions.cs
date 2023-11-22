// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Http;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eventuous.AspNetCore.Web;

public static class ResultExtensions {
    public static IResult AsResult(this Result result) {
        return result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException => AsProblem(Status409Conflict),
                AggregateNotFoundException     => AsProblem(Status404NotFound),
                DomainException                => AsValidationProblem(Status400BadRequest),
                _                              => AsProblem(Status500InternalServerError)
            }
            : Results.Ok(result);

        IResult AsProblem(int statusCode) => Results.Problem(PopulateDetails(new ProblemDetails(), error, statusCode));

        IResult AsValidationProblem(int statusCode) => Results.Problem(PopulateDetails(new ValidationProblemDetails(error.AsErrors()), error, statusCode));
    }

    public static ActionResult AsActionResult(this Result result) {
        return result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException => AsProblem(Status409Conflict),
                AggregateNotFoundException     => AsProblem(Status404NotFound),
                DomainException                => AsValidationProblem(Status400BadRequest),
                _                              => AsProblem(Status500InternalServerError)
            }
            : new OkObjectResult(result);

        ActionResult AsProblem(int statusCode) => new ObjectResult(CreateResult(new ProblemDetails(), statusCode));

        ActionResult AsValidationProblem(int statusCode) => CreateResult(new ValidationProblemDetails(error.AsErrors()), statusCode);

        ActionResult CreateResult<T>(T details, int statusCode) where T : ProblemDetails {
            details.Status = statusCode;
            details.Title  = error.ErrorMessage;
            details.Detail = error.Exception?.ToString();
            details.Type   = error.Exception?.GetType().Name;

            return new ObjectResult(details) {
                StatusCode   = Status400BadRequest,
                ContentTypes = [ContentTypes.ProblemDetails],
            };
        }
    }

    static T PopulateDetails<T>(T details, ErrorResult error, int statusCode) where T : ProblemDetails {
        details.Status = statusCode;
        details.Title  = error.ErrorMessage;
        details.Detail = error.Exception?.ToString();
        details.Type   = error.Exception?.GetType().Name;

        return details;
    }

    static IDictionary<string, string[]> AsErrors(this ErrorResult error) => new Dictionary<string, string[]> { ["Domain"] = [error.ErrorMessage] };
}
