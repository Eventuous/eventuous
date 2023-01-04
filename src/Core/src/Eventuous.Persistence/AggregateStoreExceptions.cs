namespace Eventuous;

public class OptimisticConcurrencyException : Exception {
    public OptimisticConcurrencyException(Type aggregateType, StreamName streamName, Exception inner)
        : base(
            $"Update of {aggregateType.Name} failed due to the wrong version in stream {streamName}."
          + $" {inner.Message} {inner.InnerException?.Message}"
        ) { }
}

public class OptimisticConcurrencyException<T> : OptimisticConcurrencyException
    where T : Aggregate {
    public OptimisticConcurrencyException(StreamName streamName, Exception inner)
        : base(typeof(T), streamName, inner) { }
}

public class AggregateNotFoundException : Exception {
    public AggregateNotFoundException(Type aggregateType, StreamName streamName, Exception inner)
        : base(
            $"Aggregate {aggregateType.Name} with not found in stream {streamName}. "
          + $"{inner.Message} {inner.InnerException?.Message}"
        ) { }
}

public class AggregateNotFoundException<T> : AggregateNotFoundException
    where T : Aggregate {
    public AggregateNotFoundException(StreamName streamName, Exception inner)
        : base(typeof(T), streamName, inner) { }
}
