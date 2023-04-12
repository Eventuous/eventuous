// Copyright (C) Ubiquitous AS. All rights reserved
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
    public static async Task<T> Load<T, TState, TId>(this IAggregateStore store, StreamNameMap streamNameMap, TId id, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TId : AggregateId where TState : State<TState>, new() {
        var aggregate = await store.Load<T>(streamNameMap.GetStreamName<T, TId>(id), cancellationToken).NoContext();
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
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <typeparam name="TState">State type</typeparam>
    /// <typeparam name="TId">Aggregate id type</typeparam>
    /// <returns></returns>
    public static async Task<T> LoadOrNew<T, TState, TId>(this IAggregateStore store, StreamNameMap streamNameMap, TId id, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TId : AggregateId where TState : State<TState>, new() {
        var aggregate = await store.LoadOrNew<T>(streamNameMap.GetStreamName<T, TId>(id), cancellationToken).NoContext();
        return aggregate.WithId<T, TState, TId>(id);
    }

    internal static T WithId<T, TState, TId>(this T aggregate, TId id)
        where T : Aggregate<TState>
        where TState : State<TState>, new()
        where TId : AggregateId {
        if (aggregate.State is State<TState, TId> stateWithId) {
            stateWithId.Id = id;
        }

        return aggregate;
    }
}
