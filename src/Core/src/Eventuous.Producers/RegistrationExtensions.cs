// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Producers;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

using Hosting;
using Extensions;

[PublicAPI]
public static class RegistrationExtensions {
    [Obsolete("Use AddProducer instead")]
    public static void AddEventProducer<T>(this IServiceCollection services, T producer) where T : class, IProducer {
        services.AddProducer(producer);
    }

    /// <summary>
    /// Register a producer in the DI container as IProducer using a pre-instantiated instance.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="producer">Producer instance</param>
    /// <typeparam name="T">Producer implementation type</typeparam>
    public static void AddProducer<T>(this IServiceCollection services, T producer) where T : class, IProducer {
        services.TryAddSingleton(producer);
        services.TryAddSingleton<IProducer>(sp => sp.GetRequiredService<T>());

        if (producer is IHostedService service) {
            services.TryAddSingleton(service);
        }
    }

    [Obsolete("Use AddProducer instead")]
    public static void AddEventProducer<T>(this IServiceCollection services, Func<IServiceProvider, T> getProducer) where T : class, IProducer {
        services.AddProducer(getProducer);
    }

    /// <summary>
    /// Register a producer in the DI container as IProducer using a factory function.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="getProducer">Function to resolve the producer from the service provider</param>
    /// <typeparam name="T">Producer implementation type</typeparam>
    public static void AddProducer<T>(this IServiceCollection services, Func<IServiceProvider, T> getProducer) where T : class, IProducer {
        services.TryAddSingleton(getProducer);
        AddCommon<T>(services);
    }

    [Obsolete("Use AddProducer instead")]
    public static void AddEventProducer<T>(this IServiceCollection services) where T : class, IProducer {
        services.AddProducer<T>();
    }

    /// <summary>
    /// Register a producer in the DI container as IProducer using the default constructor.
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T">Producer implementation type</typeparam>
    public static void AddProducer<T>(this IServiceCollection services) where T : class, IProducer {
        services.TryAddSingleton<T>();
        AddCommon<T>(services);
    }

    static void AddCommon<T>(IServiceCollection services) where T : class, IProducer {
        services.TryAddSingleton<IProducer>(sp => sp.GetRequiredService<T>());
        services.AddHostedServiceIfSupported<T>();
    }

    public static void AddHostedServiceIfSupported<T>(this IServiceCollection services) where T : class {
        if (typeof(T).GetInterfaces().Contains(typeof(IHostedService))) {
            // ReSharper disable once ConvertToLocalFunction
            Func<IServiceProvider, T> factory = sp => sp.GetRequiredService<T>();
            var descriptor = ServiceDescriptor.Describe(typeof(IHostedService), factory, ServiceLifetime.Singleton);
            services.TryAddEnumerable(descriptor);
        }
    }
}
