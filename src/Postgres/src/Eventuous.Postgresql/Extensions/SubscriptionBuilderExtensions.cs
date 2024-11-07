// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Registrations;

namespace Eventuous.Postgresql.Extensions;

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
    public static SubscriptionBuilder<PostgresStreamSubscription, PostgresStreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<PostgresStreamSubscription, PostgresStreamSubscriptionOptions> builder
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<PostgresStreamSubscription, PostgresStreamSubscriptionOptions, T>();

    /// <summary>
    /// Use non-default checkpoint store
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="factory">Function to resolve the checkpoint store service from the service provider</param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<PostgresStreamSubscription, PostgresStreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<PostgresStreamSubscription, PostgresStreamSubscriptionOptions> builder,
        Func<IServiceProvider, T> factory
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<PostgresStreamSubscription, PostgresStreamSubscriptionOptions, T>(factory);

    /// <summary>
    /// Use non-default checkpoint store
    /// </summary>
    /// <param name="builder"></param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions> builder
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, T>();

    /// <summary>
    /// Use non-default checkpoint store
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="factory">Function to resolve the checkpoint store service from the service provider</param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions> builder,
        Func<IServiceProvider, T> factory
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, T>(factory);
}
