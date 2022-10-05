// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using Eventuous.SqlServer.Extensions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions;

public class SqlServerStreamSubscription : SqlServerSubscriptionBase<SqlServerStreamSubscriptionOptions> {
    public SqlServerStreamSubscription(
        GetSqlServerConnection             getConnection,
        SqlServerStreamSubscriptionOptions options,
        ICheckpointStore                   checkpointStore,
        ConsumePipe                        consumePipe,
        ILoggerFactory?                    loggerFactory = null
    ) : base(getConnection, options, checkpointStore, consumePipe, loggerFactory)
        => _streamName = options.Stream.ToString();

    protected override SqlCommand PrepareCommand(SqlConnection connection, long start) {
        var cmd = new SqlCommand(Schema.ReadStreamSub, connection);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@stream_id", SqlDbType.Int, _streamId);
        cmd.Parameters.AddWithValue("@from_position", SqlDbType.Int, (int)start + 1);
        cmd.Parameters.AddWithValue("@count", SqlDbType.Int, Options.MaxPageSize);
        return cmd;
    }

    protected override async Task BeforeSubscribe(CancellationToken cancellationToken) {
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken).NoContext();
        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = Schema.CheckStream;
        cmd.Parameters.AddWithValue("@stream_name", SqlDbType.NVarChar, Options.Stream.ToString());
        cmd.Parameters.AddWithValue("@expected_version", SqlDbType.Int, -2);
        cmd.Parameters.AddOutput("@current_version", SqlDbType.Int);
        var streamId = cmd.Parameters.AddOutput("@stream_id", SqlDbType.Int);

        await cmd.ExecuteScalarAsync(cancellationToken).NoContext();
        _streamId = (int)streamId.Value;
    }

    protected override long MoveStart(PersistedEvent evt) => evt.StreamPosition;

    ulong           _sequence;
    int             _streamId;
    readonly string _streamName;

    protected override IMessageConsumeContext AsContext(
        PersistedEvent    evt,
        object?           e,
        Metadata?         meta,
        CancellationToken cancellationToken
    )
        => new MessageConsumeContext(
            evt.MessageId.ToString(),
            evt.MessageType,
            ContentType,
            _streamName,
            (ulong)evt.StreamPosition,
            (ulong)evt.GlobalPosition,
            _sequence++,
            evt.Created,
            e,
            meta,
            Options.SubscriptionId,
            cancellationToken
        );
}

public record SqlServerStreamSubscriptionOptions(StreamName Stream) : SqlServerSubscriptionBaseOptions;
