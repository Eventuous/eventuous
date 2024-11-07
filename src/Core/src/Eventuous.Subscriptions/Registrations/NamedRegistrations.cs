// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Registrations;

// ReSharper disable CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

static class NamedRegistrationExtensions {
    public static IServiceCollection AddSubscriptionBuilder<T, TOptions>(this IServiceCollection services, SubscriptionBuilder<T, TOptions> builder)
        where T : EventSubscription<TOptions> where TOptions : SubscriptionOptions {
        services.AddKeyedSingleton(builder.SubscriptionId, builder);
        // services.Add(descriptor);
        services.Configure(builder.SubscriptionId, builder.ConfigureOptions);

        return services;
    }

    public static SubscriptionBuilder<T, TOptions> GetSubscriptionBuilder<T, TOptions>(this IServiceProvider provider, string subscriptionId)
        where T : EventSubscription<TOptions> where TOptions : SubscriptionOptions {
        return provider.GetRequiredKeyedService<SubscriptionBuilder<T, TOptions>>(subscriptionId);
    }
}