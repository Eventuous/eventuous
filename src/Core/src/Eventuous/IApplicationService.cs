namespace Eventuous;

public interface IApplicationService<T> where T : Aggregate {
    Task<Result> Handle(object command, CancellationToken cancellationToken);
}

public interface IApplicationService<T, TState, TId>
    where T : Aggregate<TState, TId>
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId {
    Task<Result<TState, TId>> Handle(object command, CancellationToken cancellationToken);
}