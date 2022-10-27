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
        else {
            services.AddSingleton<IApplicationService<TAggregate>>(sp => sp.GetRequiredService<T>());
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
    public static IServiceCollection AddApplicationService<T, TAggregate, TState, TId>(
        this IServiceCollection services,
        bool                    throwOnError = false
    )
        where T : class, IApplicationService<TAggregate, TState, TId>
        where TState : State<TState>, new()
        where TId : AggregateId
        where TAggregate : Aggregate<TState> {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddSingleton<T>();

        services.AddSingleton(sp => GetThrowingService(GetTracedService(sp)));

        return services;

        IApplicationService<TAggregate, TState, TId> GetThrowingService(
            IApplicationService<TAggregate, TState, TId> inner
        )
            => throwOnError
                ? new ThrowingApplicationService<TAggregate, TState, TId>(inner)
                : inner;

        IApplicationService<TAggregate, TState, TId> GetTracedService(IServiceProvider serviceProvider)
            => EventuousDiagnostics.Enabled
                ? TracedApplicationService<TAggregate, TState, TId>.Trace(serviceProvider.GetRequiredService<T>())
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
        else {
            services.AddSingleton<IApplicationService<TAggregate>>(sp => sp.GetRequiredService<T>());
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
    public static IServiceCollection AddAggregateStore<T>(
        this IServiceCollection   services,
        Func<IServiceProvider, T> getService
    ) where T : class, IEventStore {
        services.TryAddSingleton<AggregateFactoryRegistry>();

        if (EventuousDiagnostics.Enabled) {
            services
                .AddSingleton(getService)
                .AddSingleton(sp => TracedEventStore.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.AddSingleton<IEventStore>(getService);
        }

        services.AddSingleton<IAggregateStore, AggregateStore>();
        return services;
    }

    public static IServiceCollection AddAggregateStore<T, TArchive>(this IServiceCollection services)
        where T : class, IEventStore
        where TArchive : class, IEventReader {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        
        if (EventuousDiagnostics.Enabled) {
            services
                .AddSingleton<T>()
                .AddSingleton(sp => TracedEventStore.Trace(sp.GetRequiredService<T>()));
        }
        else {
            services.AddSingleton<IEventStore, T>();
        }

        services.AddSingleton<TArchive>();
        services.AddSingleton<IAggregateStore, AggregateStore<TArchive>>();

        return services;
    }
}
