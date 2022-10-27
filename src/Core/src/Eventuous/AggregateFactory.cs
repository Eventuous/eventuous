// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;

namespace Eventuous;

/// <summary>
/// The aggregate factory registry allows customising the way how aggregate instances are
/// created by <see cref="AggregateStore"/> and <see cref="ApplicationService{T,TState,TId}"/>
/// </summary>
public class AggregateFactoryRegistry {
    /// <summary>
    /// Aggregate factory registry singleton instance 
    /// </summary>
    public static readonly AggregateFactoryRegistry Instance = new();

    readonly ConcurrentDictionary<Type, Func<dynamic>> _registry = new();

    /// <summary>
    /// Adds a custom aggregate factory to the registry
    /// </summary>
    /// <param name="factory">Function to create a given aggregate type instance</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <returns></returns>
    public AggregateFactoryRegistry CreateAggregateUsing<T>(AggregateFactory<T> factory)
        where T : Aggregate {
        _registry.TryAdd(typeof(T), () => factory());
        return this;
    }

    public void UnsafeCreateAggregateUsing<T>(Type type, Func<T> factory)
        where T : Aggregate
        => _registry.TryAdd(type, factory);

    internal T CreateInstance<T, TState>()
        where T : Aggregate<TState>
        where TState : State<TState>, new() {
        var instance = _registry.TryGetValue(typeof(T), out var factory)
            ? (T)factory()
            : Activator.CreateInstance<T>();

        return instance;
    }

    internal T CreateInstance<T>() where T : Aggregate {
        var instance = _registry.TryGetValue(typeof(T), out var factory)
            ? (T)factory()
            : Activator.CreateInstance<T>();

        return instance;
    }
}

public delegate T AggregateFactory<out T>() where T : Aggregate;
