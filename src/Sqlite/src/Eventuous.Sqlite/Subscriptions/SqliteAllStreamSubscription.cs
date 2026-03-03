// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sqlite.Projections;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.Sqlite.Subscriptions;

using Extensions;

/// <summary>
/// Subscription for all events in the system using SQLite event store.
/// </summary>
public class SqliteAllStreamSubscription(
        SqliteAllStreamSubscriptionOptions options,
        ICheckpointStore                   checkpointStore,
        ConsumePipe                        consumePipe,
        ILoggerFactory?                    loggerFactory     = null,
        IEventSerializer?                  eventSerializer   = null,
        IMetadataSerializer?               metaSerializer    = null,
        SqliteConnectionOptions?           connectionOptions = null
    )
    : SqliteSubscriptionBase<SqliteAllStreamSubscriptionOptions>(
        options,
        checkpointStore,
        consumePipe,
        SubscriptionKind.All,
        loggerFactory,
        eventSerializer,
        metaSerializer,
        connectionOptions
    ) {
    protected override SqliteCommand PrepareCommand(SqliteConnection connection, long start)
        => connection.GetTextCommand(
                $"""
                 SELECT m.message_id, m.message_type, m.stream_position, m.global_position,
                        m.json_data, m.json_metadata, m.created, s.stream_name
                 FROM {Schema.MessagesTable} m
                 INNER JOIN {Schema.StreamsTable} s ON m.stream_id = s.stream_id
                 WHERE m.global_position >= @from_position
                 ORDER BY m.global_position
                 LIMIT @count
                 """
            )
            .Add("@from_position", start + 1)
            .Add("@count", Options.MaxPageSize);
}

public record SqliteAllStreamSubscriptionOptions : SqliteSubscriptionBaseOptions;
