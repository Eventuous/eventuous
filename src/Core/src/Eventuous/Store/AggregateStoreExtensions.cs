namespace Eventuous;

[PublicAPI]
public static class AggregateStoreExtensions {
    public static Task<T> Load<T, TState, TId>(
        this IAggregateStore store,
        TId                  id,
        CancellationToken    cancellationToken
    )
        where T : Aggregate<TState, TId>, new()
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId
        => store.Load<T>(id, cancellationToken);

    public static Task<T> Load<T, TState, TId>(
        this IAggregateStore store,
        StreamName           streamName,
        CancellationToken    cancellationToken
    )
        where T : Aggregate<TState, TId>, new()
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId
        => store.Load<T>(streamName, cancellationToken);
}
