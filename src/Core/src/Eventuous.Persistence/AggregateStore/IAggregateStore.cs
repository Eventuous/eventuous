// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Aggregate state persistent store
/// </summary>
[PublicAPI]
[Obsolete("Use extensions of IEventReader and IEventWriter to load and store aggregates")]
public interface IAggregateStore {
    /// <summary>
    /// Store the new or updated aggregate state
    /// </summary>
    /// <param name="aggregate">Aggregate instance, which needs to be persisted</param>
    /// <param name="id">Aggregate id</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns>Result of the append operation</returns>
    [Obsolete("Use IEventWriter.StoreAggregate<TAggregate, TState> instead.")]
    public Task<AppendEventsResult> Store<TAggregate, TState, TId>(TAggregate aggregate, TId id, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TId : Id where TState : State<TState>, new()
        => Store<TAggregate, TState>(StreamNameFactory.For<TAggregate, TState, TId>(id), aggregate, cancellationToken);

    /// <summary>
    /// Store the new or updated aggregate state
    /// </summary>
    /// <param name="streamName"></param>
    /// <param name="aggregate">Aggregate instance, which needs to be persisted</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns>Result of the append operation</returns>
    [Obsolete("Use IEventWriter.StoreAggregate<TAggregate, TState> instead.")]
    Task<AppendEventsResult> Store<TAggregate, TState>(StreamName streamName, TAggregate aggregate, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new();

    /// <summary>
    /// Load the aggregate from the store for a given id
    /// </summary>
    /// <param name="id">Aggregate id</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <returns>Aggregate instance</returns>
    [Obsolete("Use IEventReader.LoadAggregate<TAggregate, TState> instead.")]
    public Task<TAggregate> Load<TAggregate, TState, TId>(TId id, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TId : Id where TState : State<TState>, new()
        => Load<TAggregate, TState>(StreamNameFactory.For<TAggregate, TState, TId>(id), cancellationToken);

    /// <summary>
    /// Load the aggregate from the store for a given id
    /// </summary>
    /// <param name="streamName"></param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns>Aggregate instance</returns>
    [Obsolete("Use IEventReader.LoadAggregate<TAggregate, TState> instead.")]
    Task<TAggregate> Load<TAggregate, TState>(StreamName streamName, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new();

    /// <summary>
    /// Attempts to load the aggregate from the store for a given id. If the aggregate is not found,
    /// a new instance of the aggregate is returned
    /// </summary>
    /// <param name="id">Aggregate id as string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <returns>Aggregate instance</returns>
    [Obsolete("Use IEventReader.LoadAggregate<TAggregate, TState> instead.")]
    public Task<TAggregate> LoadOrNew<TAggregate, TState, TId>(TId id, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TId : Id where TState : State<TState>, new()
        => LoadOrNew<TAggregate, TState>(StreamNameFactory.For<TAggregate, TState, TId>(id), cancellationToken);

    /// <summary>
    /// Attempts to load the aggregate from the store for a given id. If the aggregate is not found,
    /// a new instance of the aggregate is returned
    /// </summary>
    /// <param name="streamName">Name of the aggregate stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns>Aggregate instance</returns>
    [Obsolete("Use IEventReader.LoadAggregate<TAggregate, TState> instead.")]
    Task<TAggregate> LoadOrNew<TAggregate, TState>(StreamName streamName, CancellationToken cancellationToken)
        where TAggregate : Aggregate<TState> where TState : State<TState>, new();
}
