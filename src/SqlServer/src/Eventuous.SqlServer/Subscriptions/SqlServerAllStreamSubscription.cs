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

public class SqlServerAllStreamSubscription : SqlServerSubscriptionBase<SqlServerAllStreamSubscriptionOptions> {
    public SqlServerAllStreamSubscription(
        GetSqlServerConnection               getConnection,
        SqlServerAllStreamSubscriptionOptions options,
        ICheckpointStore                     checkpointStore,
        ConsumePipe                          consumePipe,
        ILoggerFactory?                      loggerFactory = null
    ) : base(getConnection, options, checkpointStore, consumePipe, loggerFactory) { }

    protected override SqlCommand PrepareCommand(SqlConnection connection, long start) {
        var cmd = new SqlCommand(Schema.ReadAllForwards, connection);

        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@from_position", SqlDbType.BigInt, start + 1);
        cmd.Parameters.AddWithValue("@count", SqlDbType.Int, Options.MaxPageSize);
        return cmd;
    }

    protected override long MoveStart(PersistedEvent evt) => evt.GlobalPosition;

    ulong _sequence;

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
            Ensure.NotEmptyString(evt.StreamName),
            (ulong)evt.GlobalPosition - 1,
            (ulong)evt.GlobalPosition - 1,
            _sequence++,
            evt.Created,
            e,
            meta,
            Options.SubscriptionId,
            cancellationToken
        );
}

public record SqlServerAllStreamSubscriptionOptions : SqlServerSubscriptionBaseOptions;
