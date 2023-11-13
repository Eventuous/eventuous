// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Producers;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

using Hosting;
using Extensions;

[PublicAPI]
public static class RegistrationExtensions {
    [Obsolete("Use AddProducer instead")]
    public static void AddEventProducer<T>(this IServiceCollection services, T producer) where T : class, IEventProducer {
        services.AddProducer(producer);
    }

    public static void AddProducer<T>(this IServiceCollection services, T producer) where T : class, IEventProducer {
        services.TryAddSingleton(producer);
        services.TryAddSingleton<IEventProducer>(sp => sp.GetRequiredService<T>());

        if (producer is IHostedService service) {
            services.TryAddSingleton(service);
        }
    }

    [Obsolete("Use AddProducer instead")]
    public static void AddEventProducer<T>(this IServiceCollection services, Func<IServiceProvider, T> getProducer) where T : class, IEventProducer {
        services.AddProducer(getProducer);
    }

    public static void AddProducer<T>(this IServiceCollection services, Func<IServiceProvider, T> getProducer) where T : class, IEventProducer {
        services.TryAddSingleton(getProducer);
        AddCommon<T>(services);
    }

    [Obsolete("Use AddProducer instead")]
    public static void AddEventProducer<T>(this IServiceCollection services) where T : class, IEventProducer {
        services.AddProducer<T>();
    }

    public static void AddProducer<T>(this IServiceCollection services) where T : class, IEventProducer {
        services.TryAddSingleton<T>();
        AddCommon<T>(services);
    }

    static void AddCommon<T>(IServiceCollection services) where T : class, IEventProducer {
        services.TryAddSingleton<IEventProducer>(sp => sp.GetRequiredService<T>());
        services.AddHostedServiceIfSupported<T>();
    }

    public static void AddHostedServiceIfSupported<T>(this IServiceCollection services) where T : class {
        if (typeof(T).GetInterfaces().Contains(typeof(IHostedService))) {
            services.TryAddSingleton(sp => (sp.GetRequiredService<T>() as IHostedService)!);
        }
    }
}
