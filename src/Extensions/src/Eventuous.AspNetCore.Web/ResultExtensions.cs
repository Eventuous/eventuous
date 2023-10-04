// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Net;
using Microsoft.AspNetCore.Http;

namespace Eventuous.AspNetCore.Web;

public static class ResultExtensions {
    public static IResult AsResult<TResult>(this Result result) where TResult : Result
        => result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException => Results.Conflict(error),
                AggregateNotFoundException     => Results.NotFound(error),
                _ => Results.Problem(
                    new ProblemDetails {
                        Status = (int?)HttpStatusCode.InternalServerError,
                        Title  = error.ErrorMessage,
                        Detail = error.Exception?.ToString(),
                        Type = error.Exception?.GetType().Name
                    }
                )
            } : Results.Ok(result);
}
