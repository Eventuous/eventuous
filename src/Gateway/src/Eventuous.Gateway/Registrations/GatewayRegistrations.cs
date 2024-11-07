// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Gateway;
using Eventuous.Subscriptions.Registrations;

// ReSharper disable CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

using Extensions;

/// <summary>
/// Registration extensions for the gateway.
/// </summary>
[PublicAPI]
public static class GatewayRegistrations {
    /// <summary>
    /// Registers a gateway subscription with a producer that has options.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="subscriptionId">Gateway subscription id. Must be unique across all subscription in the same application.</param>
    /// <param name="routeAndTransform">Routing and transformation function.</param>
    /// <param name="configureSubscription">A function to configure the subscription.</param>
    /// <param name="configureBuilder">A function to configure the subscription builder.</param>
    /// <param name="awaitProduce">An option to wait for each produce action.</param>
    /// <typeparam name="TSubscription">Subscription implementation type.</typeparam>
    /// <typeparam name="TSubscriptionOptions">Subscription options type.</typeparam>
    /// <typeparam name="TProducer">Producer implementation type.</typeparam>
    /// <typeparam name="TProduceOptions">Options for producing a message.</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>(
            this IServiceCollection                                           services,
            string                                                            subscriptionId,
            RouteAndTransform<TProduceOptions>                                routeAndTransform,
            Action<TSubscriptionOptions>?                                     configureSubscription = null,
            Action<SubscriptionBuilder<TSubscription, TSubscriptionOptions>>? configureBuilder      = null,
            bool                                                              awaitProduce          = true
        )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IProducer<TProduceOptions>
        where TProduceOptions : class
        where TSubscriptionOptions : SubscriptionOptions {
        services.TryAddSingleton<TProducer>();
        services.AddHostedServiceIfSupported<TProducer>();

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

    /// <summary>
    /// Registers a gateway subscription with a producer that has options.
    /// It expects the routing and transformation function to be registered in the service collection as <see cref="RouteAndTransform{TProduceOptions}"/>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="subscriptionId">Gateway subscription id. Must be unique across all subscription in the same application.</param>
    /// <param name="configureSubscription">A function to configure the subscription.</param>
    /// <param name="configureBuilder">A function to configure the subscription builder.</param>
    /// <param name="awaitProduce">An option to wait for each produce action.</param>
    /// <typeparam name="TSubscription">Subscription implementation type.</typeparam>
    /// <typeparam name="TSubscriptionOptions">Subscription options type.</typeparam>
    /// <typeparam name="TProducer">Producer implementation type.</typeparam>
    /// <typeparam name="TProduceOptions">Options for producing a message.</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>(
            this IServiceCollection                                           services,
            string                                                            subscriptionId,
            Action<TSubscriptionOptions>?                                     configureSubscription = null,
            Action<SubscriptionBuilder<TSubscription, TSubscriptionOptions>>? configureBuilder      = null,
            bool                                                              awaitProduce          = true
        )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IProducer<TProduceOptions>
        where TProduceOptions : class
        where TSubscriptionOptions : SubscriptionOptions {
        services.TryAddSingleton<TProducer>();
        services.AddHostedServiceIfSupported<TProducer>();

        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => {
                builder.Configure(configureSubscription);
                configureBuilder?.Invoke(builder);
                builder.AddEventHandler(GetHandler);
            }
        );

        return services;

        GatewayHandler<TProduceOptions> GetHandler(IServiceProvider sp) {
            var transform = sp.GetRequiredService<RouteAndTransform<TProduceOptions>>();
            var producer  = sp.GetRequiredService<TProducer>();

            return new(new GatewayProducer<TProduceOptions>(producer), transform, awaitProduce);
        }
    }

    /// <summary>
    /// Registers a gateway subscription with a producer that has options.
    /// It expects the routing and transformation function to be registered in the service collection as <see cref="IGatewayTransform{TProduceOptions}"/>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="subscriptionId">Gateway subscription id. Must be unique across all subscription in the same application.</param>
    /// <param name="configureSubscription">A function to configure the subscription.</param>
    /// <param name="configureBuilder">A function to configure the subscription builder.</param>
    /// <param name="awaitProduce">An option to wait for each produce action.</param>
    /// <typeparam name="TSubscription">Subscription implementation type.</typeparam>
    /// <typeparam name="TSubscriptionOptions">Subscription options type.</typeparam>
    /// <typeparam name="TProducer">Producer implementation type.</typeparam>
    /// <typeparam name="TProduceOptions">Options for producing a message.</typeparam>
    /// <typeparam name="TTransform">Message router and transformer type.</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddGateway<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions, TTransform>(
            this IServiceCollection                                           services,
            string                                                            subscriptionId,
            Action<TSubscriptionOptions>?                                     configureSubscription = null,
            Action<SubscriptionBuilder<TSubscription, TSubscriptionOptions>>? configureBuilder      = null,
            bool                                                              awaitProduce          = true
        )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IProducer<TProduceOptions>
        where TProduceOptions : class
        where TTransform : class, IGatewayTransform<TProduceOptions>
        where TSubscriptionOptions : SubscriptionOptions {
        services.TryAddSingleton<TTransform>();
        services.TryAddSingleton<TProducer>();
        services.AddHostedServiceIfSupported<TProducer>();

        services.AddSubscription<TSubscription, TSubscriptionOptions>(
            subscriptionId,
            builder => {
                builder.Configure(configureSubscription);
                configureBuilder?.Invoke(builder);
                builder.AddEventHandler(GetHandler);
            }
        );

        return services;

        GatewayHandler<TTransform, TProduceOptions> GetHandler(IServiceProvider sp) {
            var transform = sp.GetRequiredService<TTransform>();
            var producer  = sp.GetRequiredService<TProducer>();

            return new(new GatewayProducer<TProduceOptions>(producer), transform, awaitProduce);
        }
    }
}
