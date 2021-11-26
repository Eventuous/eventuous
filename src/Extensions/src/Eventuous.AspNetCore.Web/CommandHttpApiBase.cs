using Microsoft.AspNetCore.Mvc;

namespace Eventuous.AspNetCore.Web;

[PublicAPI]
public abstract class CommandHttpApiBase<T> : ControllerBase where T : Aggregate {
    readonly IApplicationService<T> _service;

    protected CommandHttpApiBase(IApplicationService<T> service) => _service = service;

    protected async Task<ActionResult<Result>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class {
        var result = await _service.Handle(command, cancellationToken);

        return result is ErrorResult error
            ? error.Exception switch {
                OptimisticConcurrencyException<T> => Conflict(error),
                AggregateNotFoundException<T>     => NotFound(error),
                _                                 => BadRequest(error)
            }
            : Ok(result);
    }
}