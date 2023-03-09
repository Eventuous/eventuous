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
