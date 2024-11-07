// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class OptimisticConcurrencyException : Exception {
    public OptimisticConcurrencyException(Type aggregateType, StreamName streamName, Exception? inner)
        : base($"Update of {aggregateType.Name} failed due to the wrong version in stream {streamName}", inner) { }

    public OptimisticConcurrencyException(StreamName streamName, Exception? inner)
        : base($"Update failed due to the wrong version in stream {streamName}", inner) { }
}

public class OptimisticConcurrencyException<T, TState>(StreamName streamName, Exception? inner) : OptimisticConcurrencyException(typeof(T), streamName, inner)
    where T : Aggregate<TState> where TState : State<TState>, new();

public class AggregateNotFoundException(Type aggregateType, StreamName streamName, Exception? inner)
    : Exception($"Aggregate {aggregateType.Name} with not found in stream {streamName}", inner);

public class AggregateNotFoundException<T, TState>(StreamName streamName, Exception? inner) : AggregateNotFoundException(typeof(T), streamName, inner)
    where T : Aggregate<TState>
    where TState : State<TState>, new();
