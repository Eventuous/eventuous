// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Aggregate state persistent store
/// </summary>
[PublicAPI]
public interface IAggregateStore {
    /// <summary>
    /// Store the new or updated aggregate state
    /// </summary>
    /// <param name="aggregate">Aggregate instance, which needs to be persisted</param>
    /// <param name="id">Aggregate id</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns></returns>
    public Task<AppendEventsResult> Store<T, TState, TId>(T aggregate, TId id, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TId : Id where TState : State<TState>, new()
        => Store<T, TState>(StreamNameFactory.For<T, TState, TId>(id), aggregate, cancellationToken);

    /// <summary>
    /// Store the new or updated aggregate state
    /// </summary>
    /// <param name="streamName"></param>
    /// <param name="aggregate">Aggregate instance, which needs to be persisted</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns></returns>
    Task<AppendEventsResult> Store<T, TState>(StreamName streamName, T aggregate, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TState : State<TState>, new();

    /// <summary>
    /// Load the aggregate from the store for a given id
    /// </summary>
    /// <param name="id">Aggregate id</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <returns></returns>
    public Task<T> Load<T, TState, TId>(TId id, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TId : Id where TState : State<TState>, new() => Load<T, TState>(StreamNameFactory.For<T, TState, TId>(id), cancellationToken);

    /// <summary>
    /// Load the aggregate from the store for a given id
    /// </summary>
    /// <param name="streamName"></param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns></returns>
    Task<T> Load<T, TState>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate<TState> where TState : State<TState>, new();

    /// <summary>
    /// Attempts to load the aggregate from the store for a given id. If the aggregate is not found,
    /// a new instance of the aggregate is returned
    /// </summary>
    /// <param name="id">Aggregate id as string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <returns></returns>
    public Task<T> LoadOrNew<T, TState, TId>(TId id, CancellationToken cancellationToken)
        where T : Aggregate<TState> where TId : Id where TState : State<TState>, new()
        => LoadOrNew<T, TState>(StreamNameFactory.For<T, TState, TId>(id), cancellationToken);

    /// <summary>
    /// Attempts to load the aggregate from the store for a given id. If the aggregate is not found,
    /// a new instance of the aggregate is returned
    /// </summary>
    /// <param name="streamName">Name of the aggregate stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns></returns>
    Task<T> LoadOrNew<T, TState>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate<TState> where TState : State<TState>, new();
}
