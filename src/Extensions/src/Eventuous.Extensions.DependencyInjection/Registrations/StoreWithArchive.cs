// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Diagnostics.Tracing;
using Eventuous.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class StoreWithArchiveRegistrations {
    /// <summary>
    /// Registers an event store service as reader, writer, and event store
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="THotStore">Implementation of <see cref="IEventStore"/> that points to the hot store</typeparam>
    /// <typeparam name="TArchiveStore">Implementation of <see cref="IEventReader"/> that points to the archive</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddEventStore<THotStore, TArchiveStore>(this IServiceCollection services)
        where THotStore : class, IEventStore
        where TArchiveStore : class, IEventReader {
        services.TryAddSingleton<THotStore>();
        services.TryAddSingleton<TArchiveStore>();
        services.AddSingleton(sp => new TieredEventStore(sp.GetRequiredService<THotStore>(), sp.GetRequiredService<TArchiveStore>()));
        AddReaderWriter(services);

        return services;
    }

    /// <summary>
    /// Registers an event store service as reader, writer, and event store
    /// </summary>
    /// <param name="services"></param>
    /// <param name="getHotStore">Function to create an instance of <see cref="IEventStore"/> that points to the hot store</param>
    /// <param name="getArchive">Function to create an instance of <see cref="IEventReader"/> that points to the archive store</param>
    /// <typeparam name="THotStore">Implementation of <see cref="IEventStore"/> that points to the hot store</typeparam>
    /// <typeparam name="TArchiveStore">Implementation of <see cref="IEventReader"/> that points to the archive</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddEventStore<THotStore, TArchiveStore>(
            this IServiceCollection               services,
            Func<IServiceProvider, THotStore>     getHotStore,
            Func<IServiceProvider, TArchiveStore> getArchive
        )
        where THotStore : class, IEventStore
        where TArchiveStore : class, IEventReader {
        services.AddSingleton(sp => new TieredEventStore(getHotStore(sp), getArchive(sp)));
        AddReaderWriter(services);

        return services;
    }

    static void AddReaderWriter(IServiceCollection services) {
        if (EventuousDiagnostics.Enabled) {
            services.AddSingleton(sp => TracedEventStore.Trace(sp.GetRequiredService<TieredEventStore>()));
        }
        else {
            services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<TieredEventStore>());
        }

        services.AddSingleton<IEventReader>(sp => sp.GetRequiredService<IEventStore>());
        services.AddSingleton<IEventWriter>(sp => sp.GetRequiredService<IEventStore>());
    }
}
