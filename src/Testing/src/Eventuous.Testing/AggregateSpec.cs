// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Testing;

/// <summary>
/// Base class for aggregate tests with a given aggregate type.
/// Operates on a given set of events and allows checking multiple assertions on the resulting state and emitted events.
/// </summary>
/// <param name="registry"></param>
/// <typeparam name="TAggregate"></typeparam>
public abstract class AggregateSpec<TAggregate>(AggregateFactoryRegistry? registry = null) where TAggregate : Aggregate {
    readonly AggregateFactoryRegistry _registry = registry ?? AggregateFactoryRegistry.Instance;

    /// <summary>
    /// Collection of events to load into the aggregate before executing the test
    /// </summary>
    /// <returns></returns>
    protected virtual object[] GivenEvents() => [];

    /// <summary>
    /// Operation to execute on the aggregate
    /// </summary>
    /// <param name="aggregate"></param>
    protected abstract void When(TAggregate aggregate);

    /// <summary>
    /// Executes the operation on the aggregate provided by <see cref="When"/> and returns the resulting aggregate instance
    /// </summary>
    /// <returns></returns>
    protected TAggregate Then() {
        var instance = _registry.CreateInstance<TAggregate>();
        instance.Load(GivenEvents());
        When(instance);

        return instance;
    }
}
