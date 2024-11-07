// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Filters.Partitioning;

using Context;

public static class Partitioner {
    /// <summary>
    /// Partition key hash calculator function
    /// </summary>
    public delegate uint GetPartitionHash(string partitionKey);

    /// <summary>
    /// Function to get a partition key from a message context
    /// </summary>
    public delegate string GetPartitionKey(IMessageConsumeContext context);
}
