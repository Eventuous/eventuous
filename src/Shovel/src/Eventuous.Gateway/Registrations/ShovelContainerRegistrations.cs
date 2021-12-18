// ReSharper disable CheckNamespace

using Eventuous.Gateway;

namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class ShovelContainerRegistrations {
    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions,
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
                    sp => new GatewayHandler<TProduceOptions>(
                        new GatewayProducer<TProduceOptions>(sp.GetRequiredService<TProducer>()),
                        routeAndTransform
                    )
                )
        );

        return services;
    }

    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions,
        TProducer, TProduceOptions>(
        this IServiceCollection       services,
        string                        subscriptionId,
        Action<TSubscriptionOptions>? configureSubscription = null
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer<TProduceOptions>
        where TProduceOptions : class
        where TSubscriptionOptions : SubscriptionOptions {
        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => builder
                .Configure(configureSubscription)
                .AddEventHandler(GetHandler)
        );

        return services;

        IEventHandler GetHandler(IServiceProvider sp) {
            var transform = sp.GetRequiredService<RouteAndTransform<TProduceOptions>>();
            var producer  = sp.GetRequiredService<TProducer>();

            return new GatewayHandler<TProduceOptions>(
                new GatewayProducer<TProduceOptions>(producer),
                transform
            );
        }
    }

    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions, TProducer>(
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
                    sp => new GatewayHandler(
                        new GatewayProducer(sp.GetRequiredService<TProducer>()),
                        routeAndTransform
                    )
                )
        );

        return services;
    }

    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions, TProducer>(
        this IServiceCollection       services,
        string                        subscriptionId,
        Action<TSubscriptionOptions>? configureSubscription = null
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer
        where TSubscriptionOptions : SubscriptionOptions {
        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => builder
                .Configure(configureSubscription)
                .AddEventHandler(GetHandler)
        );

        return services;

        IEventHandler GetHandler(IServiceProvider sp) {
            var transform = sp.GetRequiredService<RouteAndTransform>();
            var producer  = sp.GetRequiredService<TProducer>();

            return new GatewayHandler(new GatewayProducer(producer), transform);
        }
    }
}