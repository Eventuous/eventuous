// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.SqlServer.Subscriptions;

public record SqlServerStreamSubscriptionOptions : SqlServerSubscriptionBaseOptions {
    /// <summary>
    /// Stream name to subscribe for
    /// </summary>
    public StreamName Stream { get; set; }
}