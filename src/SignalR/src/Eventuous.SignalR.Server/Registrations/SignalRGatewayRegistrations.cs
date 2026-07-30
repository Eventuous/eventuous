// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.SignalR.Server;
using Microsoft.AspNetCore.SignalR;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SignalR subscription gateway services.
/// </summary>
public static class SignalRGatewayRegistrations {
    /// <summary>
    /// Registers <see cref="SubscriptionGateway{THub}"/> and its dependencies in the service collection.
    /// </summary>
    /// <typeparam name="THub">The SignalR hub type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the gateway options, primarily the <see cref="SubscriptionFactory"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSignalRSubscriptionGateway<THub>(this IServiceCollection services, Action<IServiceProvider, SignalRGatewayOptions> configure) where THub : Hub {
        services.AddSingleton(sp => {
                var options = new SignalRGatewayOptions { SubscriptionFactory = null! };
                configure(sp, options);

                return options;
            }
        );
        services.AddSingleton<SignalRProducer<THub>>();
        services.AddSingleton<SubscriptionGateway<THub>>();

        return services;
    }
}
