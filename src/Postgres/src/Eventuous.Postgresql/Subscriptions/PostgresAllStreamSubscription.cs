// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.Postgresql.Subscriptions;

using Extensions;

/// <summary>
/// Subscription for all events in the system using PostgreSQL event store.
/// </summary>
public class PostgresAllStreamSubscription : PostgresSubscriptionBase<PostgresAllStreamSubscriptionOptions> {
    public PostgresAllStreamSubscription(
        NpgsqlDataSource                     dataSource,
        PostgresAllStreamSubscriptionOptions options,
        ICheckpointStore                     checkpointStore,
        ConsumePipe                          consumePipe,
        ILoggerFactory?                      loggerFactory
    ) : base(dataSource, options, checkpointStore, consumePipe, loggerFactory) { }

    protected override NpgsqlCommand PrepareCommand(NpgsqlConnection connection, long start)
        => connection.GetCommand(Schema.ReadAllForwards)
            .Add("_from_position", NpgsqlDbType.Bigint, start + 1)
            .Add("_count", NpgsqlDbType.Integer, Options.MaxPageSize);

    protected override long MoveStart(PersistedEvent evt)
        => evt.GlobalPosition;

    ulong _sequence;

    protected override IMessageConsumeContext AsContext(PersistedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken)
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

    protected override EventPosition GetPositionFromContext(IMessageConsumeContext context)
        => EventPosition.FromAllContext(context);
}

public record PostgresAllStreamSubscriptionOptions : PostgresSubscriptionBaseOptions;
