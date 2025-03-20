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
    /// <param name="builder">Subscription builder</param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder UseCheckpointStore<T>(this SubscriptionBuilder builder)
        where T : class, ICheckpointStore {
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
    /// <param name="builder">Subscription builder</param>
    /// <param name="factory">Function to resolve the checkpoint store service from service provider</param>
    /// <typeparam name="T">Checkpoint store type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder UseCheckpointStore<T>(this SubscriptionBuilder builder, Func<IServiceProvider, T> factory)
        where T : class, ICheckpointStore {
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

    /// <summary>
    /// Use non-default serializer for the specific subscription
    /// </summary>
    /// <param name="builder">Subscription builder</param>
    /// <param name="factory">Function to create the serializer instance</param>
    /// <typeparam name="T">Serializer type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder UseSerializer<T>(this SubscriptionBuilder builder, Func<IServiceProvider, T> factory) where T : class, IEventSerializer {
        builder.Services.TryAddKeyedSingleton<IEventSerializer>(builder.SubscriptionId, (sp, _) => factory(sp));

        return builder;
    }

    /// <summary>
    /// Use non-default serializer for the specific subscription
    /// </summary>
    /// <param name="builder">Subscription builder</param>
    /// <typeparam name="T">Serializer type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder UseSerializer<T>(this SubscriptionBuilder builder) where T : class, IEventSerializer {
        builder.Services.TryAddKeyedSingleton<IEventSerializer, T>(builder.SubscriptionId);

        return builder;
    }

    /// <summary>
    /// Use non-default type mapper for the specific subscription
    /// </summary>
    /// <param name="builder">Subscription builder</param>
    /// <param name="typeMapper">Custom type mapper instance</param>
    /// <typeparam name="T">Type mapper type</typeparam>
    /// <returns></returns>
    public static SubscriptionBuilder UseTypeMapper<T>(this SubscriptionBuilder builder, T typeMapper) where T : class, ITypeMapper {
        builder.Services.TryAddKeyedSingleton<ITypeMapper>(builder.SubscriptionId, typeMapper);

        return builder;
    }
}
