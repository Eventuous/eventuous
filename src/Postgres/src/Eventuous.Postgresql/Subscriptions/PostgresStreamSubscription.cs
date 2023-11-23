// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sql.Base;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.Postgresql.Subscriptions;

using Extensions;

/// <summary>
/// Subscription for events in a single stream in PostgreSQL event store.
/// </summary>
public class PostgresStreamSubscription(
        NpgsqlDataSource                  dataSource,
        PostgresStreamSubscriptionOptions options,
        ICheckpointStore                  checkpointStore,
        ConsumePipe                       consumePipe,
        ILoggerFactory?                   loggerFactory = null
    ) : PostgresSubscriptionBase<PostgresStreamSubscriptionOptions>(dataSource, options, checkpointStore, consumePipe, loggerFactory) {
    protected override NpgsqlCommand PrepareCommand(NpgsqlConnection connection, long start)
        => connection.GetCommand(Schema.ReadStreamSub)
            .Add("_stream_id", NpgsqlDbType.Integer, _streamId)
            .Add("_from_position", NpgsqlDbType.Integer, (int)start + 1)
            .Add("_count", NpgsqlDbType.Integer, Options.MaxPageSize);

    protected override async Task BeforeSubscribe(CancellationToken cancellationToken) {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken).NoContext();

        await using var cmd = connection.GetCommand(Schema.CheckStream)
            .Add("_stream_name", NpgsqlDbType.Varchar, Options.Stream.ToString())
            .Add("_expected_version", NpgsqlDbType.Integer, -2);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();
        await reader.ReadAsync(cancellationToken).NoContext();
        _streamId = reader.GetInt32(0);
    }

    protected override long MoveStart(PersistedEvent evt) => evt.StreamPosition;

    int             _streamId;
    readonly string _streamName = options.Stream.ToString();

    protected override IMessageConsumeContext AsContext(PersistedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken)
        => new MessageConsumeContext(
            evt.MessageId.ToString(),
            evt.MessageType,
            ContentType,
            _streamName,
            (ulong)evt.StreamPosition,
            (ulong)evt.StreamPosition,
            (ulong)evt.GlobalPosition,
            Sequence++,
            evt.Created,
            e,
            meta,
            Options.SubscriptionId,
            cancellationToken
        );

    protected override EventPosition GetPositionFromContext(IMessageConsumeContext context) => EventPosition.FromContext(context);
}

public record PostgresStreamSubscriptionOptions : PostgresSubscriptionBaseOptions {
    /// <summary>
    /// Stream name to subscribe for
    /// </summary>
    public StreamName Stream { get; set; }
}
