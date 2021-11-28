using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Filters.Partitioning;

namespace Eventuous.Subscriptions.Registrations;

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
        uint                        partitionsCount,
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
    public static SubscriptionBuilder WithPartitioningByStream(
        this SubscriptionBuilder builder,
        uint                     partitionsCount
    )
        => builder.WithPartitioning(partitionsCount, ctx => ctx.Stream);
}