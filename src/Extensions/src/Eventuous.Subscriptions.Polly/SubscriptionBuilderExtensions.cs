// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Polly;

using Registrations;

[PublicAPI]
public static class SubscriptionBuilderExtensions {
    /// <summary>
    /// Adds an event handler to the subscription, adding the specified retry policy
    /// </summary>
    /// <param name="builder">Subscription builder</param>
    /// <param name="retryPolicy">Polly retry policy</param>
    /// <typeparam name="THandler">Event handler type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder AddEventHandlerWithRetries<THandler>(this SubscriptionBuilder builder, IAsyncPolicy retryPolicy)
        where THandler : class, IEventHandler
        => builder.AddCompositionEventHandler<THandler, PollyEventHandler>(h => new PollyEventHandler(h, retryPolicy));

    /// <summary>
    /// Adds an event handler to the subscription, adding the specified retry policy
    /// </summary>
    /// <param name="builder">Subscription builder</param>
    /// <param name="getHandler">Function to construct the handler</param>
    /// <param name="retryPolicy">Polly retry policy</param>
    /// <typeparam name="THandler">Event handler type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder AddEventHandlerWithRetries<THandler>(
        this SubscriptionBuilder         builder,
        Func<IServiceProvider, THandler> getHandler,
        IAsyncPolicy                     retryPolicy
    ) where THandler : class, IEventHandler
        => builder.AddCompositionEventHandler(getHandler, h => new PollyEventHandler(h, retryPolicy));
}
