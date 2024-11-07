// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.SqlServer.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Registrations;

namespace Eventuous.SqlServer.Extensions;

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
    public static SubscriptionBuilder<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions> builder
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions, T>();

    /// <summary>
    /// Use non-default checkpoint store
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="factory">Function to resolve the checkpoint store service from the service provider</param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions> builder,
        Func<IServiceProvider, T> factory
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions, T>(factory);

    /// <summary>
    /// Use non-default checkpoint store
    /// </summary>
    /// <param name="builder"></param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions> builder
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, T>();

    /// <summary>
    /// Use non-default checkpoint store
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="factory">Function to resolve the checkpoint store service from the service provider</param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions> UseCheckpointStore<T>(
        this SubscriptionBuilder<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions> builder,
        Func<IServiceProvider, T> factory
    ) where T : class, ICheckpointStore
        => builder.UseCheckpointStore<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, T>(factory);
}
