// ReSharper disable UnusedTypeParameter
namespace Eventuous;

public interface IApplicationService {
    Task<Result> Handle(object command, CancellationToken cancellationToken);
}

public interface IApplicationService<T> : IApplicationService where T : Aggregate { }

public interface IApplicationService<T, TState, TId>
    where T : Aggregate<TState, TId>
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId {
    Task<Result<TState, TId>> Handle(object command, CancellationToken cancellationToken);
}