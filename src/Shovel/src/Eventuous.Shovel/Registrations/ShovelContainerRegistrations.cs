// ReSharper disable CheckNamespace

using Eventuous.Shovel;

namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class ShovelContainerRegistrations {
    public static IServiceCollection AddShovel<TSubscription, TSubscriptionOptions,
        TProducer, TProduceOptions>(
        this IServiceCollection            services,
        string                             subscriptionId,
        RouteAndTransform<TProduceOptions> routeAndTransform,
        Action<TSubscriptionOptions>?      configureSubscription = null
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer<TProduceOptions>
        where TProduceOptions : class
        where TSubscriptionOptions : SubscriptionOptions {
        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => builder
                .Configure(configureSubscription)
                .AddEventHandler(
                    sp => new ShovelHandler<TProduceOptions>(
                        new ShovelProducer<TProduceOptions>(sp.GetRequiredService<TProducer>()),
                        routeAndTransform
                    )
                )
        );

        return services;
    }

    public static IServiceCollection AddShovel<TSubscription, TSubscriptionOptions, TProducer>(
        this IServiceCollection       services,
        string                        subscriptionId,
        RouteAndTransform             routeAndTransform,
        Action<TSubscriptionOptions>? configureSubscription = null
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer
        where TSubscriptionOptions : SubscriptionOptions {
        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => builder
                .Configure(configureSubscription)
                .AddEventHandler(
                    sp => new ShovelHandler(
                        new ShovelProducer(sp.GetRequiredService<TProducer>()),
                        routeAndTransform
                    )
                )
        );

        return services;
    }
}