namespace Eventuous;

public class OptimisticConcurrencyException : Exception {
    public OptimisticConcurrencyException(Type aggregateType, string id, Exception inner)
        : base(
            $"Update of {aggregateType.Name} with id {id} failed due to the wrong version. {inner.Message} {inner.InnerException?.Message}"
        ) { }
}

public class OptimisticConcurrencyException<T> : OptimisticConcurrencyException where T : Aggregate {
    public OptimisticConcurrencyException(T aggregate, Exception inner)
        : base(typeof(T), aggregate.GetId(), inner) { }
}

public class AggregateNotFoundException : Exception {
    public AggregateNotFoundException(Type aggregateType, string id, Exception inner)
        : base(
            $"Aggregate {aggregateType.Name} with id '{id}' not found. {inner.Message} {inner.InnerException?.Message}"
        ) { }
}

public class AggregateNotFoundException<T> : AggregateNotFoundException where T : Aggregate {
    public AggregateNotFoundException(StreamName streamName, Exception inner)
        : base(typeof(T), streamName, inner) { }
}