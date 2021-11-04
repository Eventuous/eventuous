// ReSharper disable CheckNamespace
using Eventuous.Shovel;

namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class ShovelContainerRegistrations {
    public static IServiceCollection AddShovel<TSubscription, TSubscriptionOptions,
        TProducer, TProduceOptions>(
        this IServiceCollection            services,
        string                             subscriptionId,
        RouteAndTransform<TProduceOptions> routeAndTransform
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer<TProduceOptions>
        where TProduceOptions : class
        where TSubscriptionOptions : SubscriptionOptions {
        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => builder.AddEventHandler(
                sp => new ShovelHandler<TProducer, TProduceOptions>(
                    sp.GetRequiredService<TProducer>(),
                    routeAndTransform
                )
            )
        );

        if (!services.AlreadyRegistered<ShovelProducer<TProduceOptions>>()) {
            services.AddEventProducer(
                sp => new ShovelProducer<TProduceOptions>(sp.GetRequiredService<TProducer>())
            );
        }

        return services;
    }

    public static IServiceCollection AddShovel<TSubscription, TSubscriptionOptions,
        TProducer>(
        this IServiceCollection services,
        string                  subscriptionId,
        RouteAndTransform       routeAndTransform
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer
        where TSubscriptionOptions : SubscriptionOptions {
        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => builder.AddEventHandler(
                sp => new ShovelHandler<TProducer>(
                    sp.GetRequiredService<TProducer>(),
                    routeAndTransform
                )
            )
        );

        if (!services.AlreadyRegistered<ShovelProducer>()) {
            services.AddEventProducer(
                sp => new ShovelProducer(sp.GetRequiredService<TProducer>())
            );
        }

        return services;
    }

    static bool AlreadyRegistered<T>(this IServiceCollection services)
        => services.Any(x => x.ServiceType == typeof(T));
}