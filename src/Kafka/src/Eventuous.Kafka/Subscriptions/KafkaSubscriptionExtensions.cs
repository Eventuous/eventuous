// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Kafka.Subscriptions;
using Eventuous.Subscriptions.Registrations;

// ReSharper disable CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

[PublicAPI]
public static class KafkaSubscriptionExtensions {
    /// <summary>
    /// Registers a Kafka subscription with the specified configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="subscriptionId">Unique subscription identifier</param>
    /// <param name="configureSubscription">Action to configure the subscription builder</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddKafkaSubscription(
            this IServiceCollection                                                  services,
            string                                                                   subscriptionId,
            Action<SubscriptionBuilder<KafkaBasicSubscription, KafkaSubscriptionOptions>> configureSubscription
        )
        => services.AddSubscription(subscriptionId, configureSubscription);
}
