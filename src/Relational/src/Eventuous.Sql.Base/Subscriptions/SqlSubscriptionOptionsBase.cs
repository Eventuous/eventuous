// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;

namespace Eventuous.Sql.Base.Subscriptions; 

public abstract record class SqlSubscriptionOptionsBase : SubscriptionWithCheckpointOptions {
    public string Schema           { get; set; } = "eventuous";
    public int    ConcurrencyLimit { get; set; } = 1;
    public int    MaxPageSize      { get; set; } = 1024;
}
