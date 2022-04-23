// ReSharper disable CheckNamespace

using Eventuous.Gateway;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class GatewayRegistrations {
    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions, TProducer>(
        this IServiceCollection                                           services,
        string                                                            subscriptionId,
        RouteAndTransform                                                 routeAndTransform,
        Action<TSubscriptionOptions>?                                     configureSubscription = null,
        Action<SubscriptionBuilder<TSubscription, TSubscriptionOptions>>? configureBuilder      = null,
        bool                                                              awaitProduce          = true
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer
        where TSubscriptionOptions : SubscriptionOptions {
        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => {
                builder.Configure(configureSubscription);
                configureBuilder?.Invoke(builder);

                builder.AddEventHandler(
                    sp => new GatewayHandler(
                        new GatewayProducer(sp.GetRequiredService<TProducer>()),
                        routeAndTransform,
                        awaitProduce
                    )
                );
            }
        );

        return services;
    }

    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions, TProducer>(
        this IServiceCollection                                           services,
        string                                                            subscriptionId,
        Action<TSubscriptionOptions>?                                     configureSubscription = null,
        Action<SubscriptionBuilder<TSubscription, TSubscriptionOptions>>? configureBuilder      = null,
        bool                                                              awaitProduce          = true
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer
        where TSubscriptionOptions : SubscriptionOptions {
        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => {
                builder.Configure(configureSubscription);
                configureBuilder?.Invoke(builder);
                builder.AddEventHandler(GetHandler);
            }
        );

        return services;

        IEventHandler GetHandler(IServiceProvider sp) {
            var transform = sp.GetRequiredService<RouteAndTransform>();
            var producer  = sp.GetRequiredService<TProducer>();

            return new GatewayHandler(new GatewayProducer(producer), transform, awaitProduce);
        }
    }

    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions, TProducer, TTransform>(
        this IServiceCollection                                           services,
        string                                                            subscriptionId,
        Action<TSubscriptionOptions>?                                     configureSubscription = null,
        Action<SubscriptionBuilder<TSubscription, TSubscriptionOptions>>? configureBuilder      = null,
        bool                                                              awaitProduce          = true
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer
        where TTransform : class, IGatewayTransform
        where TSubscriptionOptions : SubscriptionOptions {
        services.TryAddSingleton<TTransform>();

        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => {
                builder.Configure(configureSubscription);
                configureBuilder?.Invoke(builder);
                builder.AddEventHandler(GetHandler);
            }
        );

        return services;

        IEventHandler GetHandler(IServiceProvider sp) {
            var transform = sp.GetRequiredService<TTransform>();
            var producer  = sp.GetRequiredService<TProducer>();

            return new GatewayHandler(new GatewayProducer(producer), transform.RouteAndTransform, awaitProduce);
        }
    }
}
