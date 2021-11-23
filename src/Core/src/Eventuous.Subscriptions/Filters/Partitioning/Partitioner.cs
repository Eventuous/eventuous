using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters.Partitioning; 

public static class Partitioner {
    public delegate uint GetPartitionHash(IMessageConsumeContext context);
}