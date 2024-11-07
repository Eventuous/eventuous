// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Registrations;

namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Extensions for <see cref="SubscriptionBuilder"/>
/// </summary>
public static class SubscriptionBuilderExtensions {
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
    /// <param name="factory">Function to resolve the checkpoint store service from service provider</param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<StreamSubscription, StreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<StreamSubscription, StreamSubscriptionOptions> builder,
        Func<IServiceProvider, T> factory
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<StreamSubscription, StreamSubscriptionOptions, T>(factory);

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

    /// <summary>
    /// Use non-default checkpoint store
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="factory">Function to resolve the checkpoint store service from service provider</param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<AllStreamSubscription, AllStreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<AllStreamSubscription, AllStreamSubscriptionOptions> builder,
        Func<IServiceProvider, T> factory
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<AllStreamSubscription, AllStreamSubscriptionOptions, T>(factory);
}
