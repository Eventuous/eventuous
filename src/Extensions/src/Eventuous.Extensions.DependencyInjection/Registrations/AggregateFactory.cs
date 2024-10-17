// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class AggregateFactoryContainerExtensions {
    /// <summary>
    /// Add an aggregate factory to the container, allowing to resolve aggregate dependencies.
    /// Do not use this if your aggregate has no dependencies and has a parameterless constructor.
    /// Must be followed by "UseAggregateFactory" for IHost or IApplicationBuilder.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="createInstance">Aggregate factory function, which can get dependencies from the container.</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddAggregate<T, TState>(this IServiceCollection services, Func<IServiceProvider, T> createInstance)
        where T : Aggregate<TState> where TState : State<TState>, new() {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddSingleton(new ResolveAggregateFactory(typeof(T), createInstance));

        return services;
    }

    /// <summary>
    /// Add a default aggregate factory to the container, allowing to resolve aggregate dependencies.
    /// Do not use this if your aggregate has no dependencies and has a parameterless constructor.
    /// Must be followed by builder.UseAggregateFactory() in Startup.Configure.
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddAggregate<T, TState>(this IServiceCollection services) where T : Aggregate<TState> where TState : State<TState>, new() {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddTransient<T>();
        // ReSharper disable once ConvertToLocalFunction
        Func<IServiceProvider, T> createInstance = sp => sp.GetRequiredService<T>();

        return services.AddSingleton(new ResolveAggregateFactory(typeof(T), createInstance));
    }
}
