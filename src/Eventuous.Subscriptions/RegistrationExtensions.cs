using Eventuous.Subscriptions;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable CheckNamespace

namespace Microsoft.Extensions.DependencyInjection; 

[PublicAPI]
public static class RegistrationExtensions {
    /// <summary>
    /// Register subscription as a hosted service
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> container</param>
    /// <param name="getSubscription"></param>
    /// <typeparam name="T">Subscription service type</typeparam>
    public static IServiceCollection AddSubscription<T>(
        this IServiceCollection   services,
        Func<IServiceProvider, T> getSubscription
    ) where T : SubscriptionService {
        services.TryAddSingleton<SubscriptionHealthCheck>();
        services.AddSingleton(getSubscription);
        services.AddHostedService(sp => sp.GetRequiredService<T>());
        services.AddSingleton<IReportHealth>(sp => sp.GetRequiredService<T>());
        return services;
    }

    /// <summary>
    /// Register subscription as a hosted service
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> container</param>
    /// <typeparam name="T">Subscription service type</typeparam>
    public static IServiceCollection AddSubscription<T>(this IServiceCollection services)
        where T : SubscriptionService {
        services.TryAddSingleton<SubscriptionHealthCheck>();
        services.AddSingleton<T>();
        services.AddHostedService(sp => sp.GetRequiredService<T>());
        services.AddSingleton<IReportHealth>(sp => sp.GetRequiredService<T>());
        return services;
    }

    /// <summary>
    /// Registers event handler
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> container</param>
    /// <typeparam name="T">Event handler type</typeparam>
    public static IServiceCollection AddEventHandler<T>(this IServiceCollection services)
        where T : class, IEventHandler
        => services.AddSingleton<IEventHandler, T>();

    public static IHealthChecksBuilder AddSubscriptionsCheck(
        this IHealthChecksBuilder builder,
        string                    checkName,
        string[]                  tags
    )
        => builder.AddCheck<SubscriptionHealthCheck>(checkName, null, tags);
}