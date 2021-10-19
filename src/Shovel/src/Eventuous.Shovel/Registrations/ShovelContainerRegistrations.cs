// ReSharper disable CheckNamespace

using Eventuous.Shovel;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShovelContainerRegistrations {
    public static IServiceCollection AddShovel<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>(
        this IServiceCollection            services,
        string                             subscriptionId,
        RouteAndTransform<TProduceOptions> routeAndTransform
    )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TProducer : class, IEventProducer<TProduceOptions>
        where TProduceOptions : class
        where TSubscriptionOptions : SubscriptionOptions {
        var subscriptionBuilder = services.AddSubscription<TSubscription, TSubscriptionOptions>(subscriptionId);

        subscriptionBuilder.AddEventHandler(
            sp => new ShovelHandler<TProducer, TProduceOptions>(sp.GetRequiredService<TProducer>(), routeAndTransform)
        );

        if (!AlreadyRegistered<ShovelProducer<TProduceOptions>>()) {
            services.AddEventProducer(sp => new ShovelProducer<TProduceOptions>(sp.GetRequiredService<TProducer>()));
        }

        return services;

        bool AlreadyRegistered<T>() => services.Any(x => x.ServiceType == typeof(T));
    }
}