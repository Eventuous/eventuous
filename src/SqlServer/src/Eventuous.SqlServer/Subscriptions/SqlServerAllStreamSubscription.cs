// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.SqlServer.Projections;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions;

using Extensions;

/// <summary>
/// Subscription for all events in the system using SQL Server event store.
/// </summary>
public class SqlServerAllStreamSubscription(
        SqlServerAllStreamSubscriptionOptions options,
        ICheckpointStore                      checkpointStore,
        ConsumePipe                           consumePipe,
        ILoggerFactory?                       loggerFactory     = null,
        IEventSerializer?                     eventSerializer   = null,
        IMetadataSerializer?                  metaSerializer    = null,
        SqlServerConnectionOptions?           connectionOptions = null
    )
    : SqlServerSubscriptionBase<SqlServerAllStreamSubscriptionOptions>(
        options,
        checkpointStore,
        consumePipe,
        SubscriptionKind.All,
        loggerFactory,
        eventSerializer,
        metaSerializer,
        connectionOptions
    ) {
    protected override SqlCommand PrepareCommand(SqlConnection connection, long start)
        => connection.GetStoredProcCommand(Schema.ReadAllForwards)
            .Add("@from_position", SqlDbType.BigInt, start + 1)
            .Add("@count", SqlDbType.Int, Options.MaxPageSize);
}

public record SqlServerAllStreamSubscriptionOptions : SqlServerSubscriptionBaseOptions;
