namespace Eventuous.AspNetCore.Web;

/// <summary>
/// Base class for exposing commands via Web API using a controller.
/// </summary>
/// <typeparam name="TAggregate">Aggregate type</typeparam>
[PublicAPI]
public abstract class CommandHttpApiBase<TAggregate> : ControllerBase where TAggregate : Aggregate {
    readonly ICommandService<TAggregate> _service;

    protected CommandHttpApiBase(ICommandService<TAggregate> service) => _service = service;

    /// <summary>
    /// Call this method from your HTTP endpoints to handle commands and wrap the result properly.
    /// </summary>
    /// <param name="command">Command instance</param>
    /// <param name="cancellationToken">Request cancellation token</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns></returns>
    protected async Task<ActionResult<Result>> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class {
        var result = await _service.Handle(command, cancellationToken);
        return AsActionResult<TAggregate>(result);
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