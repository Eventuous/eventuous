namespace Eventuous;

[PublicAPI]
public record Change(object Event, string EventType);

[PublicAPI]
public record Result(object State, IEnumerable<Change>? Changes = null);

[PublicAPI]
public abstract record Result<TState, TId>(TState State, IEnumerable<Change>? Changes = null)
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId;

[PublicAPI]
public record OkResult<TState, TId>(TState State, IEnumerable<Change> Changes, ulong StreamPosition)
    : Result<TState, TId>(State, Changes)
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId;

[PublicAPI]
public record ErrorResult<TState, TId>() : Result<TState, TId>(new TState())
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId;