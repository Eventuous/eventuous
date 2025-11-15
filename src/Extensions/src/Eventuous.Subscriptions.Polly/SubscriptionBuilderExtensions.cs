// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Polly;

using System.Diagnostics.CodeAnalysis;
using Registrations;

[PublicAPI]
public static class SubscriptionBuilderExtensions {
    /// <param name="builder">Subscription builder</param>
    extension(SubscriptionBuilder builder) {
        /// <summary>
        /// Adds an event handler to the subscription, adding the specified retry policy
        /// </summary>
        /// <param name="retryPolicy">Polly retry policy</param>
        /// <typeparam name="THandler">Event handler type</typeparam>
        /// <returns></returns>
        public SubscriptionBuilder AddEventHandlerWithRetries<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
                IAsyncPolicy retryPolicy
            )
            where THandler : class, IEventHandler
            => builder.AddCompositionEventHandler<THandler, PollyEventHandler>(h => new(h, retryPolicy));

        /// <summary>
        /// Adds an event handler to the subscription, adding the specified retry policy
        /// </summary>
        /// <param name="getHandler">Function to construct the handler</param>
        /// <param name="retryPolicy">Polly retry policy</param>
        /// <typeparam name="THandler">Event handler type</typeparam>
        /// <returns></returns>
        public SubscriptionBuilder AddEventHandlerWithRetries<THandler>(
                Func<IServiceProvider, THandler> getHandler,
                IAsyncPolicy                     retryPolicy
            ) where THandler : class, IEventHandler
            => builder.AddCompositionEventHandler(getHandler, h => new PollyEventHandler(h, retryPolicy));
    }
}
