// Copyright (C) Eventuous HQ OÜ. All rights reserved
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
    /// <typeparam name="TState"></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddCommandService<T, TState>(this IServiceCollection services)
        where T : class, ICommandService<TState>
        where TState : State<TState>, new() {
        services.AddSingleton<T>();

        if (EventuousDiagnostics.Enabled) {
            services.AddSingleton(sp => TracedCommandService<TState>.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.AddSingleton<ICommandService<TState>>(sp => sp.GetRequiredService<T>());
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
    public static IServiceCollection AddCommandService<T, TState>(this IServiceCollection services, Func<IServiceProvider, T> getService)
        where T : class, ICommandService<TState> where TState : State<TState>, new() {
        services.AddSingleton(getService);

        if (EventuousDiagnostics.Enabled) {
            services.AddSingleton(sp => TracedCommandService<TState>.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.AddSingleton<ICommandService<TState>>(sp => sp.GetRequiredService<T>());
        }

        return services;
    }
}
