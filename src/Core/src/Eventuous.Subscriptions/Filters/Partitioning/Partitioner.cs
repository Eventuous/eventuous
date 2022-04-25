using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters.Partitioning; 

public static class Partitioner {
    public delegate uint GetPartitionHash(string partitionKey);
    
    public delegate string GetPartitionKey(IMessageConsumeContext context);
}