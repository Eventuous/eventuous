using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Eventuous.EventStore.Subscriptions;

public static class SubscriptionBuilderExtensions {
    public static SubscriptionBuilder<TSubscription, TOptions> UseCheckpointStore<TSubscription, TOptions, T>(
        this SubscriptionBuilder<TSubscription, TOptions> builder
    )
        where T : class, ICheckpointStore
        where TSubscription : EventStoreCatchUpSubscriptionBase<TOptions>
        where TOptions : CatchUpSubscriptionOptions {
        builder.Services.TryAddSingleton<T>();

        return EventuousDiagnostics.Enabled
            ? builder.AddParameterMap<ICheckpointStore, MeasuredCheckpointStore>(
                sp => new MeasuredCheckpointStore(sp.GetRequiredService<T>())
            )
            : builder.AddParameterMap<ICheckpointStore, T>();
    }

    public static SubscriptionBuilder<StreamSubscription, StreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<StreamSubscription, StreamSubscriptionOptions> builder
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<StreamSubscription, StreamSubscriptionOptions, T>();

    public static SubscriptionBuilder<AllStreamSubscription, AllStreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<AllStreamSubscription, AllStreamSubscriptionOptions> builder
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<AllStreamSubscription, AllStreamSubscriptionOptions, T>();
}
