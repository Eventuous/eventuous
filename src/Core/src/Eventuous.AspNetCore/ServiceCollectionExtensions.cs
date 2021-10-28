// ReSharper disable CheckNamespace
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
    public static IServiceCollection AddApplicationService<T, TAggregate>(
        this IServiceCollection services
    )
        where T : class, IApplicationService<TAggregate>
        where TAggregate : Aggregate {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddSingleton<T>();
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
        this IServiceCollection services, Func<IServiceProvider, T> getService
    )
        where T : class, IApplicationService<TAggregate>
        where TAggregate : Aggregate {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddSingleton(getService);
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
        services.AddSingleton<IEventStore, T>();
        services.AddSingleton<AggregateStore>();
        return services;
    }

    /// <summary>
    /// Registers the aggregate store using the supplied <see cref="IEventStore"/> type
    /// </summary>
    /// <param name="services"></param>
    /// <param name="getService">Function to create an instance of <see cref="IEventStore"/></param>
    /// <typeparam name="T">Implementation of <see cref="IEventStore"/></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddAggregateStore<T>(this IServiceCollection services,
        Func<IServiceProvider, T> getService)
        where T : class, IEventStore {
        services.TryAddSingleton<AggregateFactoryRegistry>();
        services.AddSingleton<IEventStore>(getService);
        services.AddSingleton<AggregateStore>();
        return services;
    }
}
