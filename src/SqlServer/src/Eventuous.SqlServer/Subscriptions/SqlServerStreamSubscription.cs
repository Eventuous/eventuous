// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions;

using Extensions;

/// <summary>
/// Subscription for events in a single stream in SQL Server event store.
/// </summary>
public class SqlServerStreamSubscription(
        SqlServerStreamSubscriptionOptions options,
        ICheckpointStore                   checkpointStore,
        ConsumePipe                        consumePipe,
        ILoggerFactory?                    loggerFactory = null
    )
    : SqlServerSubscriptionBase<SqlServerStreamSubscriptionOptions>(options, checkpointStore, consumePipe, SubscriptionKind.Stream, loggerFactory) {
    protected override SqlCommand PrepareCommand(SqlConnection connection, long start)
        => connection.GetStoredProcCommand(Schema.ReadStreamSub)
            .Add("@stream_id", SqlDbType.Int, _streamId)
            .Add("@from_position", SqlDbType.Int, (int)start + 1)
            .Add("@count", SqlDbType.Int, Options.MaxPageSize);

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
}
