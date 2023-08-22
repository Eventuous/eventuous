// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class OptimisticConcurrencyException : Exception {
    public OptimisticConcurrencyException(Type aggregateType, StreamName streamName, Exception inner)
        : base($"Update of {aggregateType.Name} failed due to the wrong version in stream {streamName}. {inner.Message} {inner.InnerException?.Message}", inner) { }

    public OptimisticConcurrencyException(StreamName streamName, Exception inner)
        : base($"Update failed due to the wrong version in stream {streamName}. {inner.Message} {inner.InnerException?.Message}", inner) { }
}

public class OptimisticConcurrencyException<T>(StreamName streamName, Exception inner) : OptimisticConcurrencyException(typeof(T), streamName, inner)
    where T : Aggregate;

public class AggregateNotFoundException : Exception {
    public AggregateNotFoundException(Type aggregateType, StreamName streamName, Exception inner)
        : base($"Aggregate {aggregateType.Name} with not found in stream {streamName}. {inner.Message} {inner.InnerException?.Message}", inner) { }
}

public class AggregateNotFoundException<T> : AggregateNotFoundException where T : Aggregate {
    public AggregateNotFoundException(StreamName streamName, Exception inner) : base(typeof(T), streamName, inner) { }
}
