// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sql.Base;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions;

using Extensions;

/// <summary>
/// Subscription for all events in the system using SQL Server event store.
/// </summary>
public class SqlServerAllStreamSubscription(
        GetSqlServerConnection                getConnection,
        SqlServerAllStreamSubscriptionOptions options,
        ICheckpointStore                      checkpointStore,
        ConsumePipe                           consumePipe,
        ILoggerFactory?                       loggerFactory = null
    )
    : SqlServerSubscriptionBase<SqlServerAllStreamSubscriptionOptions>(getConnection, options, checkpointStore, consumePipe, loggerFactory) {
    protected override SqlCommand PrepareCommand(SqlConnection connection, long start)
        => connection.GetStoredProcCommand(Schema.ReadAllForwards)
            .Add("@from_position", SqlDbType.BigInt, start + 1)
            .Add("@count", SqlDbType.Int, Options.MaxPageSize);

    protected override long MoveStart(PersistedEvent evt)
        => evt.GlobalPosition;

    ulong _sequence;

    protected override IMessageConsumeContext AsContext(PersistedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken)
        => new MessageConsumeContext(
            evt.MessageId.ToString(),
            evt.MessageType,
            ContentType,
            Ensure.NotEmptyString(evt.StreamName),
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
        => EventPosition.FromAllContext(context);
}

public record SqlServerAllStreamSubscriptionOptions : SqlServerSubscriptionBaseOptions;
