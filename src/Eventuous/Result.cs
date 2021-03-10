using System.Collections.Generic;

namespace Eventuous {
    public abstract record Result<T, TState, TId>(TState State)
        where T : Aggregate<TState, TId>
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId;

    public record OkResult<T, TState, TId>(TState State, IEnumerable<object> Changes) : Result<T, TState, TId>(State) 
        where T : Aggregate<TState, TId> 
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId;
        
    public record ErrorResult<T, TState, TId>() : Result<T, TState, TId>(new TState()) 
        where T : Aggregate<TState, TId> 
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId;
}
