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
/// Subscription for events in a single stream in the SQL Server event store.
/// </summary>
public class SqlServerStreamSubscription(
        SqlServerStreamSubscriptionOptions options,
        ICheckpointStore                   checkpointStore,
        ConsumePipe                        consumePipe,
        ILoggerFactory?                    loggerFactory     = null,
        IEventSerializer?                  eventSerializer   = null,
        IMetadataSerializer?               metaSerializer    = null,
        SqlServerConnectionOptions?        connectionOptions = null
    )
    : SqlServerSubscriptionBase<SqlServerStreamSubscriptionOptions>(
        options,
        checkpointStore,
        consumePipe,
        SubscriptionKind.Stream,
        loggerFactory,
        eventSerializer,
        metaSerializer,
        connectionOptions
    ) {
    protected override SqlCommand PrepareCommand(SqlConnection connection, long start)
        => connection.GetStoredProcCommand(Schema.ReadStreamSub)
            .Add("@stream_id", SqlDbType.Int, _streamId)
            .Add("@stream_name", SqlDbType.NVarChar, _streamName)
            .Add("@from_position", SqlDbType.Int, (int)start + 1)
            .Add("@count", SqlDbType.Int, Options.MaxPageSize);

    // Filtered by name rather than the id resolved in BeforeSubscribe, so the measure is valid before the
    // subscription connects; a stream that doesn't exist yet reads as empty.
    protected override SqlCommand PrepareEndOfStreamCommand(SqlConnection connection)
        => connection.GetTextCommand(
                $"""
                 SELECT MAX(m.StreamPosition)
                 FROM {Schema.SchemaName}.Messages m
                 INNER JOIN {Schema.SchemaName}.Streams s ON s.StreamId = m.StreamId
                 WHERE s.StreamName = @stream_name
                 """
            )
            .Add("@stream_name", SqlDbType.NVarChar, _streamName);

    protected override async Task BeforeSubscribe(CancellationToken cancellationToken) {
        await using var connection = await OpenConnection(cancellationToken).NoContext();

        await using var cmd = connection.GetStoredProcCommand(Schema.CheckStream)
            .Add("@stream_name", SqlDbType.NVarChar, Options.Stream.ToString())
            .Add("@expected_version", SqlDbType.Int, -2)
            .AddOutput("@current_version", SqlDbType.Int);

        var streamId = cmd.AddOutputParameter("@stream_id", SqlDbType.Int);

        await cmd.ExecuteScalarAsync(cancellationToken).NoContext();
        _streamId = (int)streamId.Value;
    }

    int _streamId;

    readonly string _streamName = options.Stream.ToString();
}
