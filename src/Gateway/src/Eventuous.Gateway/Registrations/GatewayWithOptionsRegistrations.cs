// ReSharper disable CheckNamespace

using Eventuous.Gateway;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class GatewayWithOptionsRegistrations {
    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>(
        this IServiceCollection                                           services,
        string                                                            subscriptionId,
        RouteAndTransform<TProduceOptions>                                routeAndTransform,
        Action<TSubscriptionOptions>?                                     configureSubscription = null,
        Action<SubscriptionBuilder<TSubscription, TSubscriptionOptions>>? configureBuilder      = null,
        bool                                                              awaitProduce          = true
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer<TProduceOptions>
        where TProduceOptions : class
        where TSubscriptionOptions : SubscriptionOptions {
        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => {
                builder.Configure(configureSubscription);
                configureBuilder?.Invoke(builder);

                builder.AddEventHandler(
                    sp => new GatewayHandler<TProduceOptions>(
                        new GatewayProducer<TProduceOptions>(sp.GetRequiredService<TProducer>()),
                        routeAndTransform,
                        awaitProduce
                    )
                );
            }
        );

        return services;
    }

    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>(
        this IServiceCollection                                           services,
        string                                                            subscriptionId,
        Action<TSubscriptionOptions>?                                     configureSubscription = null,
        Action<SubscriptionBuilder<TSubscription, TSubscriptionOptions>>? configureBuilder      = null,
        bool                                                              awaitProduce          = true
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer<TProduceOptions>
        where TProduceOptions : class
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
            var transform = sp.GetRequiredService<RouteAndTransform<TProduceOptions>>();
            var producer  = sp.GetRequiredService<TProducer>();

            return new GatewayHandler<TProduceOptions>(
                new GatewayProducer<TProduceOptions>(producer),
                transform,
                awaitProduce
            );
        }
    }

    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions,
        TTransform>(
        this IServiceCollection                                           services,
        string                                                            subscriptionId,
        Action<TSubscriptionOptions>?                                     configureSubscription = null,
        Action<SubscriptionBuilder<TSubscription, TSubscriptionOptions>>? configureBuilder      = null,
        bool                                                              awaitProduce          = true
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer<TProduceOptions>
        where TProduceOptions : class
        where TTransform : class, IGatewayTransform<TProduceOptions>
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

            return new GatewayHandler<TProduceOptions>(
                new GatewayProducer<TProduceOptions>(producer),
                transform.RouteAndTransform,
                awaitProduce
            );
        }
    }
}
