// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.Postgresql.Subscriptions;

using Extensions;

/// <summary>
/// Subscription for all events in the system using PostgreSQL event store.
/// </summary>
public class PostgresAllStreamSubscription(
        NpgsqlDataSource                     dataSource,
        PostgresAllStreamSubscriptionOptions options,
        ICheckpointStore                     checkpointStore,
        ConsumePipe                          consumePipe,
        ILoggerFactory?                      loggerFactory   = null,
        IEventSerializer?                    eventSerializer = null,
        IMetadataSerializer?                 metaSerializer  = null,
        PostgresStoreOptions?                storeOptions    = null
    )
    : PostgresSubscriptionBase<PostgresAllStreamSubscriptionOptions>(
        dataSource,
        options,
        checkpointStore,
        consumePipe,
        SubscriptionKind.All,
        loggerFactory,
        eventSerializer,
        metaSerializer,
        storeOptions
    ) {
    protected override NpgsqlCommand PrepareCommand(NpgsqlConnection connection, long start)
        => connection.GetCommand(Schema.ReadAllForwards)
            .Add("_from_position", NpgsqlDbType.Bigint, start + 1)
            .Add("_count", NpgsqlDbType.Integer, Options.MaxPageSize);
}

public record PostgresAllStreamSubscriptionOptions : PostgresSubscriptionBaseOptions;
