namespace Eventuous;

public class StreamNameMap {
    readonly Dictionary<Type, Func<AggregateId, StreamName>> _map = new();

    public void Register<T, TState, TId>(Func<TId, StreamName> map)
        where T : Aggregate<TState, TId>
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId
        => _map.TryAdd(typeof(TId), id => map((TId)id));

    public StreamName GetStreamName<T, TState, TId>(TId aggregateId)
        where T : Aggregate<TState, TId>
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId {
        if (_map.TryGetValue(typeof(TId), out var map)) return map(aggregateId);

        _map[typeof(TId)] = id => StreamName.For<T>(id);

        return _map[typeof(TId)](aggregateId);
    }
}
