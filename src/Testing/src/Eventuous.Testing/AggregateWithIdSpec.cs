// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Testing;

/// <summary>
/// Base class for aggregate tests with a given aggregate type, where aggregate state has the id.
/// Operates on a given set of events and allows checking multiple assertions on the resulting state and emitted events.
/// </summary>
/// <param name="registry">Optional: aggregate factory registry. When not provided, the default one will be used.</param>
/// <typeparam name="TAggregate">Aggregate type</typeparam>
/// <typeparam name="TState">Aggregate state type</typeparam>
/// <typeparam name="TId">Aggregate identity type</typeparam>
public abstract class AggregateWithIdSpec<TAggregate, TState, TId>(AggregateFactoryRegistry? registry = null) : AggregateSpec<TAggregate, TState>(registry)
    where TAggregate : Aggregate<TState> where TState : State<TState, TId>, new() where TId : Id {
    /// <summary>
    /// Aggregate identity value that will be set when creating a new instance for the test.
    /// </summary>
    protected abstract TId? Id { get; }

    /// <inheritdoc />
    protected override TAggregate CreateInstance() 
        => Id == null ? base.CreateInstance() : Registry.CreateTestAggregateInstance<TAggregate, TState, TId>(Id);
}
