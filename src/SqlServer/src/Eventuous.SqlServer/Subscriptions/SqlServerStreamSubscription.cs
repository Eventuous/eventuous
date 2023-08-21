// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions;

using Extensions;

/// <summary>
/// Subscription for events in a single stream in SQL Server event store.
/// </summary>
public class SqlServerStreamSubscription : SqlServerSubscriptionBase<SqlServerStreamSubscriptionOptions> {
    public SqlServerStreamSubscription(
        GetSqlServerConnection             getConnection,
        SqlServerStreamSubscriptionOptions options,
        ICheckpointStore                   checkpointStore,
        ConsumePipe                        consumePipe,
        ILoggerFactory?                    loggerFactory = null
    ) : base(getConnection, options, checkpointStore, consumePipe, loggerFactory)
        => _streamName = options.Stream.ToString();

    protected override SqlCommand PrepareCommand(SqlConnection connection, long start)
        => connection.GetStoredProcCommand(Schema.ReadStreamSub)
            .Add("@stream_id", SqlDbType.Int, _streamId)
            .Add("@from_position", SqlDbType.Int, (int)start + 1)
            .Add("@count", SqlDbType.Int, Options.MaxPageSize);

    protected override async Task BeforeSubscribe(CancellationToken cancellationToken) {
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken).NoContext();

        await using var cmd = connection.GetStoredProcCommand(Schema.CheckStream)
            .Add("@stream_name", SqlDbType.NVarChar, Options.Stream.ToString())
            .Add("@expected_version", SqlDbType.Int, -2)
            .AddOutput("@current_version", SqlDbType.Int);

        var streamId = cmd.AddOutputParameter("@stream_id", SqlDbType.Int);

        await cmd.ExecuteScalarAsync(cancellationToken).NoContext();
        _streamId = (int)streamId.Value;
    }

    protected override long MoveStart(PersistedEvent evt)
        => evt.StreamPosition;

    ulong           _sequence;
    int             _streamId;
    readonly string _streamName;

    protected override IMessageConsumeContext AsContext(PersistedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken)
        => new MessageConsumeContext(
            evt.MessageId.ToString(),
            evt.MessageType,
            ContentType,
            _streamName,
            (ulong)evt.StreamPosition,
            (ulong)evt.StreamPosition,
            (ulong)evt.GlobalPosition,
            _sequence++,
            evt.Created,
            e,
            meta,
            Options.SubscriptionId,
            cancellationToken
        );

    protected override EventPosition GetPositionFromContext(IMessageConsumeContext context)
        => EventPosition.FromContext(context);
}