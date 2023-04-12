// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Filters.Partitioning;

using Context;

public static class Partitioner {
    public delegate uint GetPartitionHash(string partitionKey);

    public delegate string GetPartitionKey(IMessageConsumeContext context);
}
