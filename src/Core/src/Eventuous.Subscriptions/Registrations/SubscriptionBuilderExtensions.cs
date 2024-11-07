// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Eventuous.Subscriptions.Registrations;

using Filters;
using Filters.Partitioning;
using Checkpoints;

public static class SubscriptionBuilderExtensions {
    /// <summary>
    /// Adds partitioning to the subscription. Keep in mind that not all subscriptions can support partitioned consume.
    /// </summary>
    /// <param name="builder">Subscription builder</param>
    /// <param name="partitionsCount">Number of partitions</param>
    /// <param name="getPartitionKey">Function to get the partition key from the context</param>
    /// <returns></returns>
    [PublicAPI]
    public static SubscriptionBuilder WithPartitioning(this SubscriptionBuilder builder, int partitionsCount, Partitioner.GetPartitionKey getPartitionKey)
        => builder.AddConsumeFilterFirst(new PartitioningFilter(partitionsCount, getPartitionKey));

    /// <summary>
    /// Adds partitioning to the subscription using the stream name as partition key.
    /// Keep in mind that not all subscriptions can support partitioned consume.
    /// </summary>
    /// <param name="builder">Subscription builder</param>
    /// <param name="partitionsCount">Number of partitions</param>
    /// <returns></returns>
    [PublicAPI]
    public static SubscriptionBuilder WithPartitioningByStream(this SubscriptionBuilder builder, int partitionsCount)
        => builder.WithPartitioning(partitionsCount, ctx => ctx.Stream);

    /// <summary>
    /// Use non-default checkpoint store for the specific subscription
    /// </summary>
    /// <param name="builder"></param>
    /// <typeparam name="TSubscription">Subscription type</typeparam>
    /// <typeparam name="TOptions">Subscription options type</typeparam>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<TSubscription, TOptions> UseCheckpointStore<TSubscription, TOptions, T>(this SubscriptionBuilder<TSubscription, TOptions> builder)
        where T : class, ICheckpointStore
        where TSubscription : EventSubscriptionWithCheckpoint<TOptions>
        where TOptions : SubscriptionWithCheckpointOptions {
        builder.Services.TryAddKeyedSingleton<T>(builder.SubscriptionId);

        if (EventuousDiagnostics.Enabled) {
            builder.Services.TryAddKeyedSingleton<ICheckpointStore>(
                builder.SubscriptionId,
                (sp, key) => new MeasuredCheckpointStore(sp.GetRequiredKeyedService<T>(key))
            );
        }
        else {
            builder.Services.TryAddKeyedSingleton<ICheckpointStore, T>(builder.SubscriptionId);
        }

        return builder;
    }

    /// <summary>
    /// Use non-default checkpoint store for the specific subscription
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="factory">Function to resolve the checkpoint store service from service provider</param>
    /// <typeparam name="TSubscription">Subscription type</typeparam>
    /// <typeparam name="TOptions">Subscription options type</typeparam>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder<TSubscription, TOptions> UseCheckpointStore<TSubscription, TOptions, T>(
            this SubscriptionBuilder<TSubscription, TOptions> builder,
            Func<IServiceProvider, T>                         factory
        )
        where T : class, ICheckpointStore
        where TSubscription : EventSubscriptionWithCheckpoint<TOptions>
        where TOptions : SubscriptionWithCheckpointOptions {
        if (EventuousDiagnostics.Enabled) {
            builder.Services.TryAddKeyedSingleton<ICheckpointStore>(
                builder.SubscriptionId,
                (sp, _) => new MeasuredCheckpointStore(factory(sp))
            );
        }
        else {
            builder.Services.TryAddKeyedSingleton<ICheckpointStore>(builder.SubscriptionId, (sp, _) => factory(sp));
        }

        return builder;
    }
}
