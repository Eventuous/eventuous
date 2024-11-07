// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Testing; 

public static class AggregateFactoryExtensions {
    /// <summary>
    /// Creates an instance of the aggregate and assigns the aggregate ID
    /// </summary>
    /// <param name="registry">Aggregate factory registry</param>
    /// <param name="id">Aggregate identity</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <returns></returns>
    [Obsolete("This overload is for backwards compability. Use CreateTestAggregateInstance that uses Id in stead of AggregateId as TId parameter.")]
    public static TAggregate CreateTestAggregateInstanceForAggregateId<TAggregate, TState, TId>(this AggregateFactoryRegistry registry, TId id) 
        where TAggregate : Aggregate<TState> where TState : State<TState>, new() where TId : AggregateId
        => registry.CreateInstance<TAggregate, TState>().WithId<TAggregate, TState, TId>(id);

    /// <summary>
    /// Creates an instance of the aggregate and assigns the ID of the aggregate
    /// </summary>
    /// <param name="registry">Aggregate factory registry</param>
    /// <param name="id">Aggregate identity</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <returns></returns>
    public static TAggregate CreateTestAggregateInstance<TAggregate, TState, TId>(this AggregateFactoryRegistry registry, TId id)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new() where TId : Id
        => registry.CreateInstance<TAggregate, TState>().WithId<TAggregate, TState, TId>(id);
}
