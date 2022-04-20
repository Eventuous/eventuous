using Microsoft.AspNetCore.Http;

namespace Eventuous.AspNetCore.Web; 

public static class ResultExtensions {
    public static IResult AsResult(this Result result)
        => result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException => Results.Conflict(error),
                AggregateNotFoundException     => Results.NotFound(error),
                _                              => Results.BadRequest(error)
            } : Results.Ok(result);

    
}
