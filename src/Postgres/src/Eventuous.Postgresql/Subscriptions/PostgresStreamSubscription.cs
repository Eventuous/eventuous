// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.Postgresql.Subscriptions;

using Extensions;

/// <summary>
/// Subscription for events in a single stream in the PostgreSQL event store.
/// </summary>
public class PostgresStreamSubscription(
        NpgsqlDataSource                  dataSource,
        PostgresStreamSubscriptionOptions options,
        ICheckpointStore                  checkpointStore,
        ConsumePipe                       consumePipe,
        ILoggerFactory?                   loggerFactory   = null,
        IEventSerializer?                 eventSerializer = null,
        IMetadataSerializer?              metaSerializer  = null,
        PostgresStoreOptions?             storeOptions    = null
    ) : PostgresSubscriptionBase<PostgresStreamSubscriptionOptions>(
    dataSource,
    options,
    checkpointStore,
    consumePipe,
    SubscriptionKind.Stream,
    loggerFactory,
    eventSerializer,
    metaSerializer,
    storeOptions
) {
    protected override NpgsqlCommand PrepareCommand(NpgsqlConnection connection, long start)
        => connection.GetCommand(Schema.ReadStreamSub)
            .Add("_stream_id", NpgsqlDbType.Integer, _streamId)
            .Add("_stream_name", NpgsqlDbType.Varchar, _streamName)
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

    int _streamId;

    readonly string _streamName = options.Stream.ToString();
}

public record PostgresStreamSubscriptionOptions : PostgresSubscriptionBaseOptions {
    /// <summary>
    /// Stream name to subscribe for
    /// </summary>
    public StreamName Stream { get; set; }
}
