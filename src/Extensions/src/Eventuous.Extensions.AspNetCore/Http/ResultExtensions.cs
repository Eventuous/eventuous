// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Http;
using static Microsoft.AspNetCore.Http.StatusCodes;

// ReSharper disable once CheckNamespace
namespace Eventuous.Extensions.AspNetCore;

static class ResultExtensions {
    public static IResult AsResult<TState>(this Result<TState> result) where TState : State<TState>, new() {
        return result.Match(
            Results.Ok,
            error => error.Exception switch {
                OptimisticConcurrencyException => AsProblem(error, Status409Conflict),
                AggregateNotFoundException     => AsProblem(error, Status404NotFound),
                DomainException                => AsValidationProblem(error, Status400BadRequest),
                _                              => AsProblem(error, Status500InternalServerError)
            }
        );

        static IResult AsProblem(Result<TState>.Error error, int statusCode)
            => Results.Problem(PopulateDetails(new ProblemDetails(), error, statusCode));

        static IResult AsValidationProblem(Result<TState>.Error error, int statusCode)
            => Results.Problem(PopulateDetails(new ValidationProblemDetails(error.AsErrors()), error, statusCode));
    }

    public static ActionResult AsActionResult<TState>(this Result<TState> result) where TState : State<TState>, new() {
        return result.Match(
            ok => new OkObjectResult(ok),
            error =>
                error.Exception switch {
                    OptimisticConcurrencyException => AsProblem(error, Status409Conflict),
                    AggregateNotFoundException     => AsProblem(error, Status404NotFound),
                    DomainException                => AsValidationProblem(error, Status400BadRequest),
                    _                              => AsProblem(error, Status500InternalServerError)
                }
        );

        static ActionResult AsProblem(Result<TState>.Error error, int statusCode) => CreateResult(error, new ProblemDetails(), statusCode);

        static ActionResult AsValidationProblem(Result<TState>.Error error, int statusCode)
            => CreateResult(error, new ValidationProblemDetails(error.AsErrors()), statusCode);

        static ActionResult CreateResult<T>(Result<TState>.Error error, T details, int statusCode) where T : ProblemDetails
            => new ObjectResult(PopulateDetails(details, error, statusCode)) {
                StatusCode   = statusCode,
                ContentTypes = [ContentTypes.ProblemDetails]
            };
    }

    static T PopulateDetails<T, TState>(T details, Result<TState>.Error error, int statusCode) where T : ProblemDetails where TState : State<TState>, new() {
        details.Status = statusCode;
        details.Title  = error.ErrorMessage;
        details.Detail = error.Exception?.ToString();
        details.Type   = error.Exception?.GetType().Name;

        return details;
    }

    static Dictionary<string, string[]> AsErrors<TState>(this Result<TState>.Error error) where TState : State<TState>, new()
        => new() { ["Domain"] = [error.ErrorMessage] };
}
