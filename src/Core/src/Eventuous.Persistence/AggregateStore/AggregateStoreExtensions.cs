// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class AggregateStoreExtensions {
    /// <summary>
    /// Loads an aggregate by its ID, assigns the State.Id property
    /// </summary>
    /// <param name="store">Aggregate store instance</param>
    /// <param name="streamNameMap">Stream name map</param>
    /// <param name="id">Aggregate id</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <typeparam name="TState">State type</typeparam>
    /// <typeparam name="TId">Aggregate id type</typeparam>
    /// <returns></returns>
    [Obsolete("Use IEventReader.LoadAggregates instead.")]
    public static async Task<T> Load<T, TState, TId>(this IAggregateStore store, StreamNameMap streamNameMap, TId id, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TId : Id where TState : State<TState>, new() {
        var aggregate = await store.Load<T, TState>(streamNameMap.GetStreamName<T, TState, TId>(id), cancellationToken).NoContext();

        return aggregate.WithId<T, TState, TId>(id);
    }

    /// <summary>
    /// Loads an aggregate by its ID, assigns the State.Id property.
    /// If the aggregate stream is not found, returns a new aggregate instance
    /// </summary>
    /// <param name="store">Aggregate store instance</param>
    /// <param name="streamNameMap">Stream name map</param>
    /// <param name="id">Aggregate id</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">State type</typeparam>
    /// <typeparam name="TId">Aggregate id type</typeparam>
    /// <returns></returns>
    [Obsolete("Use IEventReader.LoadAggregates instead.")]
    public static async Task<TAggregate> LoadOrNew<TAggregate, TState, TId>(
            this IAggregateStore store,
            StreamNameMap        streamNameMap,
            TId                  id,
            CancellationToken    cancellationToken
        )
        where TAggregate : Aggregate<TState> where TId : Id where TState : State<TState>, new() {
        var aggregate = await store.LoadOrNew<TAggregate, TState>(streamNameMap.GetStreamName<TAggregate, TState, TId>(id), cancellationToken).NoContext();

        return aggregate.WithId<TAggregate, TState, TId>(id);
    }

    internal static TAggregate WithId<TAggregate, TState, TId>(this TAggregate aggregate, TId id)
        where TAggregate : Aggregate<TState>
        where TState : State<TState>, new()
        where TId : Id {
        if (aggregate.State is State<TState, TId> stateWithId) {
            stateWithId.Id = id;
        }

        return aggregate;
    }
}
