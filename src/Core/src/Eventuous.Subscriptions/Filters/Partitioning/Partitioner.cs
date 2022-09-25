// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters.Partitioning; 

public static class Partitioner {
    public delegate uint GetPartitionHash(string partitionKey);
    
    public delegate string GetPartitionKey(IMessageConsumeContext context);
}