// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// The aggregate factory registry allows customizing the way how aggregate instances are
/// created by <see cref="AggregateStore"/> and <see langword="CommandService{T,TState,TId}"/>
/// </summary>
public class AggregateFactoryRegistry {
    /// <summary>
    /// Aggregate factory registry singleton instance 
    /// </summary>
    public static readonly AggregateFactoryRegistry Instance = new();
    
    private AggregateFactoryRegistry() { }

    [UsedImplicitly]
    public AggregateFactoryRegistry(IServiceProvider sp, IEnumerable<ResolveAggregateFactory> resolvers) {
        foreach (var resolver in resolvers) {
            UnsafeCreateAggregateUsing(resolver.Type, () => resolver.CreateInstance(sp));
        }
    }

    internal readonly Dictionary<Type, Func<object>> Registry = new();

    /// <summary>
    /// Adds a custom aggregate factory to the registry
    /// </summary>
    /// <param name="factory">Function to create a given aggregate type instance</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns></returns>
    public AggregateFactoryRegistry CreateAggregateUsing<T, TState>(AggregateFactory<T, TState> factory)
        where T : Aggregate<TState> where TState : State<TState>, new() {
        Registry.TryAdd(typeof(T), () => factory());

        return this;
    }

    public void UnsafeCreateAggregateUsing(Type type, Func<object> factory) => Registry.TryAdd(type, factory);

    public T CreateInstance<T, TState>() where T : Aggregate<TState> where TState : State<TState>, new() {
        var instance = Registry.TryGetValue(typeof(T), out var factory) ? (T)factory() : Activator.CreateInstance<T>();

        return instance;
    }
}

public delegate T AggregateFactory<out T, TState>() where T : Aggregate<TState> where TState : State<TState>, new();

public record ResolveAggregateFactory(Type Type, Func<IServiceProvider, object> CreateInstance);
