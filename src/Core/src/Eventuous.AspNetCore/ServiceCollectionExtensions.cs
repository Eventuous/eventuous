// ReSharper disable CheckNamespace

using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Tracing;

namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Registers the application service in the container
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TAggregate"></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddApplicationService<T, TAggregate>(this IServiceCollection services)
        where T : class, IApplicationService<TAggregate>
        where TAggregate : Aggregate {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddSingleton<T>();

        if (EventuousDiagnostics.Enabled) {
            services.AddSingleton(
                sp => TracedApplicationService<TAggregate>.Trace(sp.GetRequiredService<T>())
            );
        }

        return services;
    }

    /// <summary>
    /// Registers the application service in the container
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T">Application service implementation type</typeparam>
    /// <typeparam name="TState">Aggregate state type</typeparam>
    /// <typeparam name="TId">Aggregate identity type</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddApplicationService<T, TState, TId>(this IServiceCollection services)
        where T : class, IApplicationService<TState, TId>
        where TState : AggregateState<TState, TId>, new()
        where TId : AggregateId {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddSingleton<T>();

        if (EventuousDiagnostics.Enabled) {
            services.AddSingleton(
                sp => TracedApplicationService<TState, TId>.Trace(sp.GetRequiredService<T>())
            );
        }

        return services;
    }

    /// <summary>
    /// Registers the application service in the container
    /// </summary>
    /// <param name="services"></param>
    /// <param name="getService">Function to create an app service instance</param>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TAggregate"></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddApplicationService<T, TAggregate>(
        this IServiceCollection   services,
        Func<IServiceProvider, T> getService
    )
        where T : class, IApplicationService<TAggregate>
        where TAggregate : Aggregate {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddSingleton(getService);

        if (EventuousDiagnostics.Enabled) {
            services.AddSingleton(
                sp => TracedApplicationService<TAggregate>.Trace(sp.GetRequiredService<T>())
            );
        }

        return services;
    }

    /// <summary>
    /// Registers the aggregate store using the supplied <see cref="IEventStore"/> type
    /// </summary>
    /// <param name="services"></param>
    /// <typeparam name="T">Implementation of <see cref="IEventStore"/></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddAggregateStore<T>(this IServiceCollection services)
        where T : class, IEventStore {
        services.TryAddSingleton<AggregateFactoryRegistry>();

        if (EventuousDiagnostics.Enabled) {
            services
                .AddSingleton<T>()
                .AddSingleton(sp => TracedEventStore.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.AddSingleton<IEventStore, T>();
        }

        services.AddSingleton<AggregateStore>();
        services.AddSingleton<IAggregateStore>(sp => sp.GetRequiredService<AggregateStore>());
        return services;
    }

    /// <summary>
    /// Registers the aggregate store using the supplied <see cref="IEventStore"/> type
    /// </summary>
    /// <param name="services"></param>
    /// <param name="getService">Function to create an instance of <see cref="IEventStore"/></param>
    /// <typeparam name="T">Implementation of <see cref="IEventStore"/></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddAggregateStore<T>(
        this IServiceCollection   services,
        Func<IServiceProvider, T> getService
    )
        where T : class, IEventStore {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        
        if (EventuousDiagnostics.Enabled) {
            services
                .AddSingleton(getService)
                .AddSingleton(sp => TracedEventStore.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.AddSingleton<IEventStore>(getService);
        }
        services.AddSingleton<AggregateStore>();
        return services;
    }
}