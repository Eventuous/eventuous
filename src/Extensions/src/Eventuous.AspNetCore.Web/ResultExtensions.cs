using Microsoft.AspNetCore.Mvc;

namespace Eventuous.AspNetCore.Web;

public static class ResultExtensions {
    public static ActionResult<Result> AsActionResult<T>(this Result result) where T : Aggregate
        => result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException<T> => new ConflictObjectResult(error),
                AggregateNotFoundException<T>     => new NotFoundObjectResult(error),
                _                                 => new BadRequestObjectResult(error)
            }
            : new OkObjectResult(result);
}