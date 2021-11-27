using Microsoft.AspNetCore.Mvc;

namespace Eventuous.AspNetCore.Web;

[PublicAPI]
public abstract class CommandHttpApiBase<T> : ControllerBase where T : Aggregate {
    readonly IApplicationService<T> _service;

    protected CommandHttpApiBase(IApplicationService<T> service) => _service = service;

    protected async Task<ActionResult<Result>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class {
        var result = await _service.Handle(command, cancellationToken);
        return AsActionResult<T>(result);
    }
    
    static ActionResult<Result> AsActionResult<T>(Result result) where T : Aggregate
        => result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException<T> => new ConflictObjectResult(error),
                AggregateNotFoundException<T>     => new NotFoundObjectResult(error),
                _                                 => new BadRequestObjectResult(error)
            }
            : new OkObjectResult(result);
}