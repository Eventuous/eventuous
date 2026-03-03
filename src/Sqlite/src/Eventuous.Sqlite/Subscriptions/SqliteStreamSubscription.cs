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
/// Subscription for events in a single stream in the SQLite event store.
/// </summary>
public class SqliteStreamSubscription(
        SqliteStreamSubscriptionOptions options,
        ICheckpointStore                checkpointStore,
        ConsumePipe                     consumePipe,
        ILoggerFactory?                 loggerFactory     = null,
        IEventSerializer?               eventSerializer   = null,
        IMetadataSerializer?            metaSerializer    = null,
        SqliteConnectionOptions?        connectionOptions = null
    )
    : SqliteSubscriptionBase<SqliteStreamSubscriptionOptions>(
        options,
        checkpointStore,
        consumePipe,
        SubscriptionKind.Stream,
        loggerFactory,
        eventSerializer,
        metaSerializer,
        connectionOptions
    ) {
    protected override SqliteCommand PrepareCommand(SqliteConnection connection, long start)
        => connection.GetTextCommand(
                $"""
                 SELECT m.message_id, m.message_type, m.stream_position, m.global_position,
                        m.json_data, m.json_metadata, m.created, @stream_name AS stream_name
                 FROM {Schema.MessagesTable} m
                 WHERE m.stream_id = @stream_id AND m.stream_position >= @from_position
                 ORDER BY m.global_position
                 LIMIT @count
                 """
            )
            .Add("@stream_id", _streamId)
            .Add("@stream_name", _streamName)
            .Add("@from_position", (int)start + 1)
            .Add("@count", Options.MaxPageSize);

    protected override async Task BeforeSubscribe(CancellationToken cancellationToken) {
        await using var connection = await OpenConnection(cancellationToken).NoContext();

        await using var ensureCmd = connection.GetTextCommand(
            $"INSERT OR IGNORE INTO {Schema.StreamsTable} (stream_name, version) VALUES (@stream_name, -1)"
        ).Add("@stream_name", Options.Stream.ToString());

        await ensureCmd.ExecuteNonQueryAsync(cancellationToken).NoContext();

        await using var selectCmd = connection.GetTextCommand(
            $"SELECT stream_id FROM {Schema.StreamsTable} WHERE stream_name = @stream_name"
        ).Add("@stream_name", Options.Stream.ToString());

        var result = await selectCmd.ExecuteScalarAsync(cancellationToken).NoContext();
        _streamId = Convert.ToInt32(result);
    }

    int _streamId;

    readonly string _streamName = options.Stream.ToString();
}

public record SqliteStreamSubscriptionOptions : SqliteSubscriptionBaseOptions {
    public StreamName Stream { get; set; }
}
