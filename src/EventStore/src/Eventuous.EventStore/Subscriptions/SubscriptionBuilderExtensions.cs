using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Eventuous.EventStore.Subscriptions; 

public static class SubscriptionBuilderExtensions {
    public static SubscriptionBuilder<StreamSubscription, StreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<StreamSubscription, StreamSubscriptionOptions> builder
    ) where T : class, ICheckpointStore {
        builder.Services.TryAddSingleton<T>();
        return builder.AddParameterMap<ICheckpointStore, T>();
    }
}