// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable CheckNamespace

using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Tracing;

namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static partial class ServiceCollectionExtensions {
    /// <summary>
    /// Registers the aggregate store using the supplied <see cref="IEventStore"/> type
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T">Implementation of <see cref="IEventStore"/></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddAggregateStore<T>(this IServiceCollection services) where T : class, IEventStore {
        services.TryAddSingleton<AggregateFactoryRegistry>();

        if (EventuousDiagnostics.Enabled) {
            services.TryAddSingleton<T>();
            services.TryAddSingleton(sp => TracedEventStore.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.TryAddSingleton<IEventStore, T>();
        }

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
    public static IServiceCollection AddAggregateStore<T>(this IServiceCollection services, Func<IServiceProvider, T> getService) where T : class, IEventStore {
        services.TryAddSingleton<AggregateFactoryRegistry>();

        if (EventuousDiagnostics.Enabled) {
            services.TryAddSingleton(getService);
            services.TryAddSingleton(sp => TracedEventStore.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.TryAddSingleton<IEventStore>(getService);
        }

        services.AddSingleton<IAggregateStore, AggregateStore>();
        return services;
    }

    public static IServiceCollection AddAggregateStore<T, TArchive>(this IServiceCollection services) where T : class, IEventStore where TArchive : class, IEventReader {
        services.TryAddSingleton<AggregateFactoryRegistry>();

        if (EventuousDiagnostics.Enabled) {
            services.TryAddSingleton<T>();
            services.TryAddSingleton(sp => TracedEventStore.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.TryAddSingleton<IEventStore, T>();
        }

        services.TryAddSingleton<TArchive>();
        services.AddSingleton<IAggregateStore, AggregateStore<TArchive>>();

        return services;
    }

    /// <summary>
    /// Registers the event reader
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T">Implementation of <see cref="IEventReader"/></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddEventReader<T>(this IServiceCollection services) where T : class, IEventReader {
        if (EventuousDiagnostics.Enabled) {
            services.TryAddSingleton<T>();
            services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.TryAddSingleton<IEventReader, T>();
        }

        return services;
    }

    /// <summary>
    /// Registers the event reader
    /// </summary>
    /// <param name="services"></param>
    /// <param name="getService">Function to create an instance of <see cref="IEventReader"/></param>
    /// <typeparam name="T">Implementation of <see cref="IEventReader"/></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddEventReader<T>(this IServiceCollection services, Func<IServiceProvider, T> getService) where T : class, IEventReader {
        if (EventuousDiagnostics.Enabled) {
            services.TryAddSingleton(getService);
            services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.TryAddSingleton<IEventReader>(getService);
        }

        return services;
    }

    /// <summary>
    /// Registers the event writer
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T">Implementation of <see cref="IEventWriter"/></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddEventWriter<T>(this IServiceCollection services) where T : class, IEventWriter {
        if (EventuousDiagnostics.Enabled) {
            services.TryAddSingleton<T>();
            services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.TryAddSingleton<IEventWriter, T>();
        }

        return services;
    }

    /// <summary>
    /// Registers the event writer
    /// </summary>
    /// <param name="services"></param>
    /// <param name="getService">Function to create an instance of <see cref="IEventWriter"/></param>
    /// <typeparam name="T">Implementation of <see cref="IEventWriter"/></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddEventWriter<T>(this IServiceCollection services, Func<IServiceProvider, T> getService) where T : class, IEventWriter {
        if (EventuousDiagnostics.Enabled) {
            services.TryAddSingleton(getService);
            services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.TryAddSingleton<IEventWriter>(getService);
        }

        return services;
    }

    /// <summary>
    /// Registers the event reader and writer
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T">Implementation of <see cref="IEventWriter"/> and <see cref="IEventReader"/></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddEventReaderWriter<T>(this IServiceCollection services) where T : class, IEventWriter, IEventReader {
        if (EventuousDiagnostics.Enabled) {
            services.TryAddSingleton<T>();
            services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
            services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.TryAddSingleton<IEventReader, T>();
            services.TryAddSingleton<IEventWriter, T>();
        }

        return services;
    }

    /// <summary>
    /// Registers the event reader and writer implemented by one class
    /// </summary>
    /// <param name="services"></param>
    /// <param name="getService">Function to create an instance of the class,
    /// which implements both <see cref="IEventReader"/> and <see cref="IEventWriter"/></param>
    /// <typeparam name="T">Implementation of <see cref="IEventWriter"/></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddEventReaderWriter<T>(this IServiceCollection services, Func<IServiceProvider, T> getService) where T : class, IEventWriter, IEventReader {
        if (EventuousDiagnostics.Enabled) {
            services.TryAddSingleton(getService);
            services.TryAddSingleton(sp => TracedEventReader.Trace(sp.GetRequiredService<T>()));
            services.TryAddSingleton(sp => TracedEventWriter.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.TryAddSingleton<IEventWriter>(getService);
            services.TryAddSingleton<IEventReader>(getService);
        }

        return services;
    }
}
