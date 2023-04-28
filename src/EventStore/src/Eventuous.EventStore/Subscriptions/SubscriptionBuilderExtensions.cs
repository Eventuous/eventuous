using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Extensions for <see cref="SubscriptionBuilder"/>
/// </summary>
public static class SubscriptionBuilderExtensions {
    /// <summary>
    /// Use non-default checkpoint store
    /// </summary>
    /// <param name="builder"></param>
    /// <typeparam name="TSubscription">Subscription type</typeparam>
    /// <typeparam name="TOptions">Subscription options type</typeparam>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
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

    /// <summary>
    /// Use non-default checkpoint store
    /// </summary>
    /// <param name="builder"></param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<StreamSubscription, StreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<StreamSubscription, StreamSubscriptionOptions> builder
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<StreamSubscription, StreamSubscriptionOptions, T>();

    /// <summary>
    /// Use non-default checkpoint store
    /// </summary>
    /// <param name="builder"></param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<AllStreamSubscription, AllStreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<AllStreamSubscription, AllStreamSubscriptionOptions> builder
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<AllStreamSubscription, AllStreamSubscriptionOptions, T>();
}
