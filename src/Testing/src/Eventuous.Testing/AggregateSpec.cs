// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Shouldly;

namespace Eventuous.Testing;

/// <summary>
/// Base class for aggregate tests with a given aggregate type.
/// Operates on a given set of events and allows checking multiple assertions on the resulting state and emitted events.
/// </summary>
/// <param name="registry">Optional: aggregate factory registry. When not provided, the default one will be used.</param>
/// <typeparam name="TAggregate">Aggregate type</typeparam>
/// <typeparam name="TState">Aggregate state type</typeparam>
public abstract class AggregateSpec<TAggregate, TState>(AggregateFactoryRegistry? registry = null)
    where TAggregate : Aggregate<TState> where TState : State<TState>, new() {
    protected readonly AggregateFactoryRegistry Registry = registry ?? AggregateFactoryRegistry.Instance;

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
    /// Function to create aggregate instances.
    /// </summary>
    /// <returns>Aggregate instance creating using the aggregate factory</returns>
    protected virtual TAggregate CreateInstance() => Registry.CreateInstance<TAggregate, TState>();

    /// <summary>
    /// Executes the operation on the aggregate provided by <see cref="When"/> and returns the resulting aggregate instance
    /// </summary>
    /// <returns></returns>
    [MemberNotNull(nameof(Instance))]
    protected TAggregate Then() {
        Instance = CreateInstance();
        Instance.Load(GivenEvents());
        When(Instance);

        return Instance;
    }

    /// <summary>
    /// Checks if one or more events were emitted after the operation.
    /// </summary>
    /// <param name="events">Events to verify</param>
    /// <returns>Aggregate instance for further inspection</returns>
    // ReSharper disable once UnusedMethodReturnValue.Global
    [StackTraceHidden]
    protected TAggregate Emitted(params object[] events) {
        if (Instance == null) {
            Then();
        }

        events.ShouldBeSubsetOf(Instance.Changes);

        return Instance;
    }

    protected TAggregate? Instance { get; private set; }
}
