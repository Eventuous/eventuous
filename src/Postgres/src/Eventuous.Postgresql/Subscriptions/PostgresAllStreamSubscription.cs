// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Eventuous.Postgresql.Subscriptions;

public class PostgresAllStreamSubscription : PostgresSubscriptionBase<PostgresAllStreamSubscriptionOptions> {
    public PostgresAllStreamSubscription(
        GetPostgresConnection                getConnection,
        PostgresAllStreamSubscriptionOptions options,
        ICheckpointStore                     checkpointStore,
        ConsumePipe                          consumePipe,
        ILoggerFactory?                      loggerFactory
    ) : base(getConnection, options, checkpointStore, consumePipe, loggerFactory) { }

    protected override NpgsqlCommand PrepareCommand(NpgsqlConnection connection, long start) {
        var cmd = new NpgsqlCommand(Schema.ReadAllForwards, connection);

        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("_from_position", NpgsqlDbType.Bigint, start + 1);
        cmd.Parameters.AddWithValue("_count", NpgsqlDbType.Integer, Options.MaxPageSize);
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

public record PostgresAllStreamSubscriptionOptions : PostgresSubscriptionBaseOptions;
