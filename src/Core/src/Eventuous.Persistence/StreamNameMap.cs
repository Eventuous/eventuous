// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace Eventuous;

/// <summary>
/// Provides a mapping mechanism for generating stream names from aggregate identifiers.
/// Allows custom stream name generation strategies to be registered for different identifier types.
/// </summary>
public class StreamNameMap {
    readonly TypeMap<Func<Id, StreamName>> _typeMap = new();

    /// <summary>
    /// Registers a custom stream name mapping function for a specific identifier type.
    /// </summary>
    /// <typeparam name="TId">The type of the identifier that inherits from <see cref="Id"/>.</typeparam>
    /// <param name="map">A function that maps an identifier of type <typeparamref name="TId"/> to a <see cref="StreamName"/>.</param>
    public void Register<TId>(Func<TId, StreamName> map) where TId : Id => _typeMap.Add<TId>(id => map((TId)id));

    /// <summary>
    /// Gets the stream name for an aggregate with a specific identifier.
    /// Uses the registered mapping function if available, otherwise falls back to the default factory.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate that inherits from <see cref="Aggregate{TState}"/>.</typeparam>
    /// <typeparam name="TState">The type of the aggregate state that inherits from <see cref="State{TState}"/>.</typeparam>
    /// <typeparam name="TId">The type of the aggregate identifier that inherits from <see cref="Id"/>.</typeparam>
    /// <param name="aggregateId">The aggregate identifier to map to a stream name.</param>
    /// <returns>A <see cref="StreamName"/> for the specified aggregate identifier.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public StreamName GetStreamName<T, TState, TId>(TId aggregateId) where TId : Id where T : Aggregate<TState> where TState : State<TState>, new()
        => _typeMap.TryGetValue<TId>(out var map) ? map(aggregateId) : StreamNameFactory.For<T, TState, TId>(aggregateId);

    /// <summary>
    /// Gets the stream name for a specific identifier.
    /// Uses the registered mapping function if available, otherwise falls back to the default factory.
    /// </summary>
    /// <typeparam name="TId">The type of the identifier that inherits from <see cref="Id"/>.</typeparam>
    /// <param name="id">The identifier to map to a stream name.</param>
    /// <returns>A <see cref="StreamName"/> for the specified identifier.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public StreamName GetStreamName<TId>(TId id) where TId : Id => _typeMap.TryGetValue<TId>(out var map) ? map(id) : StreamNameFactory.For(id);
}