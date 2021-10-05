namespace Eventuous;

public interface IApplicationService<T> where T : Aggregate {
    Task<Result> Handle<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : class;
}

public interface IApplicationService<TState, TId>
    where TState : AggregateState<TState, TId>, new() where TId : AggregateId {
    Task<Result<TState, TId>> Handle<TCommand>(
        TCommand          command,
        CancellationToken cancellationToken
    ) where TCommand : class;
}
