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
public record ErrorResult<TState, TId> : Result<TState, TId>
    where TState : AggregateState<TState, TId>, new()
    where TId : AggregateId {
    
    public ErrorResult(string message, Exception? exception) : base(new TState()) {
        Message   = message;
        Exception = exception;
    }

    public ErrorResult(Exception exception) : base(new TState()) {
        Exception = Ensure.NotNull(exception, nameof(exception));
        Message   = exception.Message;
    }

    public string     Message   { get; init; }
    public Exception? Exception { get; init; }
}