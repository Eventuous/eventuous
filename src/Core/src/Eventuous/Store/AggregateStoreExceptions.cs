namespace Eventuous;

public class OptimisticConcurrencyException<T> : Exception where T : Aggregate {
    public OptimisticConcurrencyException(T aggregate, Exception inner)
        : base(
            $"Update of {aggregate.GetId()} failed due to the wrong version. {inner.Message} {inner.InnerException?.Message}"
        ) { }
}

public class AggregateNotFoundException<T> : Exception where T : Aggregate {
    public AggregateNotFoundException(string id, Exception inner)
        : base(
            $"Aggregate {typeof(T).Name} with id '{id}' not found. {inner.Message} {inner.InnerException?.Message}"
        ) { }
}