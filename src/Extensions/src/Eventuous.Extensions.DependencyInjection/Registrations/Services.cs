// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable CheckNamespace

using Eventuous.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions {
    /// <summary>
    /// Registers the application service in the container
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TAggregate"></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddCommandService<T, TAggregate>(this IServiceCollection services) where T : class, ICommandService<TAggregate> where TAggregate : Aggregate {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddSingleton<T>();

        if (EventuousDiagnostics.Enabled) {
            services.AddSingleton(sp => TracedCommandService<TAggregate>.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.AddSingleton<ICommandService<TAggregate>>(sp => sp.GetRequiredService<T>());
        }

        return services;
    }

    /// <summary>
    /// Registers the application service in the container
    /// </summary>
    /// <param name="services"></param>
    /// <param name="throwOnError">Set to true if you want the app service to throw instead of returning the error result</param>
    /// <typeparam name="T">Application service implementation type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <typeparam name="TAggregate">Aggregate type</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddCommandService<T, TAggregate, TState, TId>(this IServiceCollection services, bool throwOnError = false)
        where T : class, ICommandService<TAggregate, TState, TId>
        where TState : State<TState>, new()
        where TId : Id
        where TAggregate : Aggregate<TState> {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddSingleton<T>();
        services.AddSingleton(sp => GetThrowingService(GetTracedService(sp)));
        return services;

        ICommandService<TAggregate, TState, TId> GetThrowingService(ICommandService<TAggregate, TState, TId> inner)
            => throwOnError
                ? new ThrowingCommandService<TAggregate, TState, TId>(inner)
                : inner;

        ICommandService<TAggregate, TState, TId> GetTracedService(IServiceProvider serviceProvider)
            => EventuousDiagnostics.Enabled
                ? TracedCommandService<TAggregate, TState, TId>.Trace(serviceProvider.GetRequiredService<T>())
                : serviceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Registers the application service in the container
    /// </summary>
    /// <param name="services"></param>
    /// <param name="getService">Function to create an app service instance</param>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TAggregate"></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddCommandService<T, TAggregate>(this IServiceCollection services, Func<IServiceProvider, T> getService)
        where T : class, ICommandService<TAggregate>
        where TAggregate : Aggregate {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddSingleton(getService);

        if (EventuousDiagnostics.Enabled) {
            services.AddSingleton(sp => TracedCommandService<TAggregate>.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.AddSingleton<ICommandService<TAggregate>>(sp => sp.GetRequiredService<T>());
        }

        return services;
    }

    /// <summary>
    /// Registers the application service in the container
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TState"></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddFunctionalService<T, TState>(this IServiceCollection services)
        where T : class, IFuncCommandService<TState>
        where TState : State<TState>, new() {
        services.AddSingleton<T>();

        if (EventuousDiagnostics.Enabled) {
            services.AddSingleton(sp => TracedFunctionalService<TState>.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.AddSingleton<IFuncCommandService<TState>>(sp => sp.GetRequiredService<T>());
        }

        return services;
    }

    /// <summary>
    /// Registers the application service in the container
    /// </summary>
    /// <param name="services"></param>
    /// <param name="getService">Function to create an app service instance</param>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TState"></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddFunctionalService<T, TState>(this IServiceCollection services, Func<IServiceProvider, T> getService)
        where T : class, IFuncCommandService<TState> where TState : State<TState>, new() {
        services.AddSingleton(getService);

        if (EventuousDiagnostics.Enabled) {
            services.AddSingleton(sp => TracedFunctionalService<TState>.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.AddSingleton<IFuncCommandService<TState>>(sp => sp.GetRequiredService<T>());
        }

        return services;
    }
}
