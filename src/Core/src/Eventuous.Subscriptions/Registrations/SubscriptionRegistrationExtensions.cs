using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ReSharper disable CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class SubscriptionRegistrationExtensions {
    public static ISubscriptionBuilder AddSubscription<T, TOptions>(
        this IServiceCollection services,
        string                  subscriptionId,
        Action<TOptions>?       configureOptions = null,
        IEnumerable<string>?    healthTags       = null
    )
        where T : EventSubscription<TOptions>
        where TOptions : SubscriptionOptions {
        ISubscriptionBuilder<T, TOptions> builder = new DefaultSubscriptionBuilder<T, TOptions>(
            Ensure.NotNull(services, nameof(services)),
            Ensure.NotEmptyString(subscriptionId, nameof(subscriptionId))
        );

        services.AddSubscriptionBuilder(builder);

        services.Configure<TOptions>(subscriptionId, ConfigureOptions);

        services.AddSingleton(sp => GetBuilder(sp).ResolveSubscription(sp));
        

        services.AddSingleton<IHostedService>(
            sp =>
                new SubscriptionHostedService(
                    GetBuilder(sp).ResolveSubscription(sp),
                    sp.GetService<ISubscriptionHealth>(),
                    sp.GetService<ILoggerFactory>()
                )
        );

        if (typeof(IMeasuredSubscription).IsAssignableFrom(typeof(T)))
            services.AddSingleton(GetGapMeasure);

        return builder;

        void ConfigureOptions(TOptions options) {
            options.SubscriptionId = subscriptionId;
            configureOptions?.Invoke(options);
        }

        ISubscriptionBuilder<T, TOptions> GetBuilder(IServiceProvider sp)
            => sp.GetSubscriptionBuilder<T, TOptions>(subscriptionId);

        ISubscriptionGapMeasure GetGapMeasure(IServiceProvider sp) {
             var subscription = GetBuilder(sp).ResolveSubscription(sp) as IMeasuredSubscription;
             return subscription!.GetMeasure();
        }
    }

    public static ISubscriptionBuilder AddEventHandler<THandler>(this ISubscriptionBuilder builder)
        where THandler : class, IEventHandler {
        builder.Services.AddSingleton<THandler>();
        builder.Services.AddSingleton<ResolveHandler>(Resolve);
        return builder;

        IEventHandler? Resolve(IServiceProvider sp, string id)
            => id == builder.SubscriptionId ? sp.GetService<THandler>() : null;
    }

    public static ISubscriptionBuilder AddEventHandler<THandler>(
        this ISubscriptionBuilder        builder,
        Func<IServiceProvider, THandler> getHandler
    )
        where THandler : class, IEventHandler {
        builder.Services.AddSingleton(getHandler);
        builder.Services.AddSingleton<ResolveHandler>(Resolve);
        return builder;

        IEventHandler? Resolve(IServiceProvider sp, string id)
            => id == builder.SubscriptionId ? sp.GetService<THandler>() : null;
    }

    /// <summary>
    /// Adds a health check for subscriptions. All subscriptions will be monitored by one check.
    /// </summary>
    /// <param name="builder">Health checks builder</param>
    /// <param name="checkName">Name of the health check</param>
    /// <param name="failureStatus">Health status for unhealthy subscriptions</param>
    /// <param name="tags">Health check tags list</param>
    /// <returns></returns>
    public static IHealthChecksBuilder AddSubscriptionsCheck(
        this IHealthChecksBuilder builder,
        string                    checkName,
        HealthStatus?             failureStatus,
        string[]                  tags
    ) {
        builder.Services.TryAddSingleton<SubscriptionHealthCheck>();
        builder.Services.TryAddSingleton<ISubscriptionHealth>(sp => sp.GetRequiredService<SubscriptionHealthCheck>());
        return builder.AddCheck<SubscriptionHealthCheck>(checkName, failureStatus, tags);
    }

    public static IServiceCollection AddCheckpointStore<T>(this IServiceCollection services)
        where T : class, ICheckpointStore {
        services.AddSingleton<T>();
        services.AddSingleton<ICheckpointStore>(sp => new MeasuredCheckpointStore(sp.GetRequiredService<T>()));
        return services;
    }
}