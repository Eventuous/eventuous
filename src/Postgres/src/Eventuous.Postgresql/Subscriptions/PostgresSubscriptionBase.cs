// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sql.Base.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.Postgresql.Subscriptions;

public abstract class PostgresSubscriptionBase<T>(
        NpgsqlDataSource      dataSource,
        T                     options,
        ICheckpointStore      checkpointStore,
        ConsumePipe           consumePipe,
        SubscriptionKind      kind,
        ILoggerFactory?       loggerFactory,
        IEventSerializer?     eventSerializer,
        IMetadataSerializer?  metaSerializer,
        PostgresStoreOptions? storeOptions
    )
    : SqlSubscriptionBase<T, NpgsqlConnection>(
        options,
        checkpointStore,
        consumePipe,
        options.ConcurrencyLimit,
        kind,
        loggerFactory,
        eventSerializer,
        metaSerializer
    )
    where T : PostgresSubscriptionBaseOptions {
    protected Schema Schema { get; } = new(
        storeOptions?.Schema is not null and not Schema.DefaultSchema
            ? storeOptions.Schema
            : options.Schema
    );

    protected NpgsqlDataSource DataSource { get; } = dataSource;

    protected override async ValueTask<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken)
        => await DataSource.OpenConnectionAsync(cancellationToken).NoContext();

    protected override bool IsTransient(Exception exception) => exception is PostgresException { IsTransient: true };

    protected override string GetEndOfStream { get; } = $"select max(stream_position) from {options.Schema}.messages";
    protected override string GetEndOfAll    { get; } = $"select max(global_position) from {options.Schema}.messages";
}

public abstract record PostgresSubscriptionBaseOptions : SqlSubscriptionOptionsBase {
    protected PostgresSubscriptionBaseOptions() {
        Schema = Postgresql.Schema.DefaultSchema;
    }
}
