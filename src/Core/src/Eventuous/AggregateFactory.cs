using System.Collections.Concurrent;

namespace Eventuous;

/// <summary>
/// The aggregate factory registry allows customising the way how aggregate instances are
/// created by <see cref="AggregateStore"/> and <see cref="ApplicationService{T,TState,TId}"/>
/// </summary>
[PublicAPI]
public class AggregateFactoryRegistry {
    /// <summary>
    /// Aggregate factory registry singleton instance 
    /// </summary>
    public static readonly AggregateFactoryRegistry Instance = new();

    readonly ConcurrentDictionary<Type, Func<Aggregate>> _registry = new();

    /// <summary>
    /// Adds a custom aggregate factory to the registry
    /// </summary>
    /// <param name="factory">Function to create a given aggregate type instance</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public AggregateFactoryRegistry CreateAggregateUsing<T>(AggregateFactory<T> factory)
        where T : Aggregate {
        _registry.TryAdd(typeof(T), () => factory());
        return this;
    }

    public void UnsafeCreateAggregateUsing(Type type, Func<Aggregate> factory) => _registry.TryAdd(type, factory);

    internal T CreateInstance<T, TState, TId>()
        where T : Aggregate<TState, TId>
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId => _registry.TryGetValue(typeof(T), out var factory)
        ? (T)factory()
        : Activator.CreateInstance<T>();

    internal T CreateInstance<T>() where T : Aggregate
        => _registry.TryGetValue(typeof(T), out var factory)
            ? (T)factory()
            : Activator.CreateInstance<T>();
}

public delegate T AggregateFactory<out T>() where T : Aggregate;