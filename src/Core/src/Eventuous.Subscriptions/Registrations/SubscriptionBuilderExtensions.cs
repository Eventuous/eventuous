// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Registrations;

using Filters;
using Filters.Partitioning;

public static class SubscriptionBuilderExtensions {
    /// <summary>
    /// Adds partitioning to the subscription. Keep in mind that not all subscriptions can support partitioned consume.
    /// </summary>
    /// <param name="builder">Subscription builder</param>
    /// <param name="partitionsCount">Number of partitions</param>
    /// <param name="getPartitionKey">Function to get the partition key from the context</param>
    /// <returns></returns>
    [PublicAPI]
    public static SubscriptionBuilder WithPartitioning(
        this SubscriptionBuilder    builder,
        int                         partitionsCount,
        Partitioner.GetPartitionKey getPartitionKey
    )
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
}
