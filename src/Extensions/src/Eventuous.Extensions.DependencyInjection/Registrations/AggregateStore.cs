// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Diagnostics.Tracing;
using Eventuous.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class AggregateStoreRegistrationExtensions {
    /// <summary>
    /// Registers the aggregate store using the supplied <see cref="IEventStore"/> type
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T">Implementation of <see cref="IEventStore"/></typeparam>
    /// <returns></returns>
    [Obsolete("Use AddEventStore instead.")]
    public static IServiceCollection AddAggregateStore<T>(this IServiceCollection services) where T : class, IEventStore {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.TryAddSingleton<T>();

        if (EventuousDiagnostics.Enabled) { services.TryAddSingleton(sp => TracedEventStore.Trace(sp.GetRequiredService<T>())); }
        else { services.TryAddSingleton<IEventStore>(sp => sp.GetRequiredService<T>()); }

        services.AddSingleton<IAggregateStore, AggregateStore>();

        return services;
    }

    /// <summary>
    /// Registers the aggregate store using the supplied <see cref="IEventStore"/> type
    /// </summary>
    /// <param name="services"></param>
    /// <param name="getService">Function to create an instance of <see cref="IEventStore"/></param>
    /// <typeparam name="T">Implementation of <see cref="IEventStore"/></typeparam>
    /// <returns></returns>
    [Obsolete("Use AddEventStore instead.")]
    public static IServiceCollection AddAggregateStore<T>(this IServiceCollection services, Func<IServiceProvider, T> getService) where T : class, IEventStore {
        services.TryAddSingleton<AggregateFactoryRegistry>();

        if (EventuousDiagnostics.Enabled) {
            services.TryAddSingleton(getService);
            services.TryAddSingleton(sp => TracedEventStore.Trace(sp.GetRequiredService<T>()));
        }
        else { services.TryAddSingleton<IEventStore>(getService); }

        services.AddSingleton<IAggregateStore, AggregateStore>();

        return services;
    }

    [Obsolete("Use AddEventStore and TieredEventReader instead.")]
    public static IServiceCollection AddAggregateStore<T, TArchive>(this IServiceCollection services)
        where T : class, IEventStore where TArchive : class, IEventReader {
        services.TryAddSingleton<AggregateFactoryRegistry>();

        if (EventuousDiagnostics.Enabled) {
            services.TryAddSingleton<T>();
            services.TryAddSingleton(sp => TracedEventStore.Trace(sp.GetRequiredService<T>()));
        }
        else { services.TryAddSingleton<IEventStore, T>(); }

        services.TryAddSingleton<TArchive>();
        services.AddSingleton<IAggregateStore, AggregateStore<TArchive>>();

        return services;
    }
    
}
