using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Eventuous.Subscriptions.Polly;

[PublicAPI]
public static class SubscriptionBuilderExtensions {
    /// <summary>
    /// Adds an event handler to the subscription, adding the specified retry policy
    /// </summary>
    /// <param name="builder">Subscription builder</param>
    /// <param name="retryPolicy">Polly retry policy</param>
    /// <typeparam name="THandler">Event handler type</typeparam>
    /// <returns></returns>
    public static ISubscriptionBuilder AddEventHandlerWithRetries<THandler>(
        this ISubscriptionBuilder builder,
        IAsyncPolicy              retryPolicy
    ) where THandler : class, IEventHandler {
        builder.Services.AddSingleton<THandler>();

        builder.Services.AddSingleton<ResolveHandler>(
            (sp, id) => Resolve<THandler>(builder.SubscriptionId, sp, id, retryPolicy)
        );

        return builder;
    }

    /// <summary>
    /// Adds an event handler to the subscription, adding the specified retry policy
    /// </summary>
    /// <param name="builder">Subscription builder</param>
    /// <param name="getHandler">Function to construct the handler</param>
    /// <param name="retryPolicy">Polly retry policy</param>
    /// <typeparam name="THandler">Event handler type</typeparam>
    /// <returns></returns>
    public static ISubscriptionBuilder AddEventHandlerWithRetries<THandler>(
        this ISubscriptionBuilder        builder,
        Func<IServiceProvider, THandler> getHandler,
        IAsyncPolicy                     retryPolicy
    )
        where THandler : class, IEventHandler {
        builder.Services.AddSingleton(getHandler);

        builder.Services.AddSingleton<ResolveHandler>(
            (sp, id) => Resolve<THandler>(builder.SubscriptionId, sp, id, retryPolicy)
        );

        return builder;
    }

    static IEventHandler? Resolve<THandler>(
        string           subscriptionId,
        IServiceProvider sp,
        string           id,
        IAsyncPolicy     retryPolicy
    ) where THandler : class, IEventHandler
        => id == subscriptionId ? new PollyEventHandler(
            sp.GetRequiredService<THandler>(),
            retryPolicy
        ) : null;
}