// ReSharper disable CheckNamespace

using Eventuous.Producers;
using Eventuous.Subscriptions;

namespace Microsoft.Extensions.DependencyInjection;

public static class ConnectorRegistration {
    public static IServiceCollection
        AddConnector<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>(
            this IServiceCollection services,
            Func<ConnectorBuilder, ConnectorBuilder<TSubscription, TSubscriptionOptions, TProducer, TProduceOptions>>
                configure
        )
        where TSubscription : EventSubscription<TSubscriptionOptions>
        where TSubscriptionOptions : SubscriptionOptions
        where TProducer : class, IEventProducer<TProduceOptions>
        where TProduceOptions : class {
        var builder = configure(new ConnectorBuilder());
        builder.Register(services);
        return services;
    }
}
