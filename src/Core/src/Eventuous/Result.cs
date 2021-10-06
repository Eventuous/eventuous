namespace Eventuous;

public record Result(object State, IEnumerable<object>? Changes = null);

[PublicAPI]
public abstract record Result<TState, TId>(TState State, IEnumerable<object>? Changes = null)
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId;

[PublicAPI]
public record OkResult<TState, TId>(TState State, IEnumerable<object> Changes, ulong StreamPosition)
    : Result<TState, TId>(State, Changes)
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId;

[PublicAPI]
public record ErrorResult<TState, TId>() : Result<TState, TId>(new TState())
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId;