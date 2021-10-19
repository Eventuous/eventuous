// ReSharper disable CheckNamespace

using Eventuous.Subscriptions;

namespace Microsoft.Extensions.DependencyInjection;

static class NamedRegistrationExtensions {
    public static void AddSubscriptionBuilder<T, TOptions>(
        this IServiceCollection           services,
        ISubscriptionBuilder<T, TOptions> builder
    ) where T : EventSubscription<TOptions> where TOptions : SubscriptionOptions {
        if (services.Any(x => x is NamedDescriptor named && named.Name == builder.SubscriptionId)) {
            throw new InvalidOperationException(
                $"Existing subscription builder with id {builder.SubscriptionId} already registered"
            );
        }

        var descriptor = new NamedDescriptor(
            builder.SubscriptionId,
            typeof(ISubscriptionBuilder<T, TOptions>),
            builder
        );

        services.Add(descriptor);
    }

    public static ISubscriptionBuilder<T, TOptions> GetSubscriptionBuilder<T, TOptions>(
        this IServiceProvider provider,
        string                subscriptionId
    ) where T : EventSubscription<TOptions> where TOptions : SubscriptionOptions {
        var services = provider.GetServices<ISubscriptionBuilder<T, TOptions>>();
        return services.Single(x => x.SubscriptionId == subscriptionId);
    }
}

class NamedDescriptor : ServiceDescriptor {
    public string Name { get; }

    public NamedDescriptor(string name, Type serviceType, object instance) : base(serviceType, instance) => Name = name;
}