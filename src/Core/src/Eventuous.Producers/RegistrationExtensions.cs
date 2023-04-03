// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Producers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class RegistrationExtensions {
    public static void AddEventProducer<T>(this IServiceCollection services, T producer)
        where T : class, IEventProducer {
        services.TryAddSingleton(producer);
        services.TryAddSingleton<IEventProducer>(sp => sp.GetRequiredService<T>());
        if (producer is IHostedService service) {
            services.TryAddSingleton(service);
        }
    }

    public static void AddEventProducer<T>(this IServiceCollection services, Func<IServiceProvider, T> getProducer)
        where T : class, IEventProducer {
        services.TryAddSingleton(getProducer);
        AddCommon<T>(services);
    }

    public static void AddEventProducer<T>(this IServiceCollection services)
        where T : class, IEventProducer {
        services.TryAddSingleton<T>();
        AddCommon<T>(services);
    }

    static void AddCommon<T>(IServiceCollection services)
        where T : class, IEventProducer {
        services.TryAddSingleton<IEventProducer>(sp => sp.GetRequiredService<T>());
        services.AddHostedServiceIfSupported<T>();
    }

    public static void AddHostedServiceIfSupported<T>(this IServiceCollection services) where T : class {
        if (typeof(T).GetInterfaces().Contains(typeof(IHostedService))) {
            services.TryAddSingleton(sp => (sp.GetRequiredService<T>() as IHostedService)!);
        }
    }
}