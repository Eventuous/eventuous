// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sql.Base.Subscriptions;
using Eventuous.Sqlite.Projections;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.Sqlite.Subscriptions;

public abstract class SqliteSubscriptionBase<T> : SqlSubscriptionBase<T, SqliteConnection> where T : SqliteSubscriptionBaseOptions {
    protected Schema Schema { get; }
    readonly  string _connectionString;

    protected SqliteSubscriptionBase(
            T                        options,
            ICheckpointStore         checkpointStore,
            ConsumePipe              consumePipe,
            SubscriptionKind         kind,
            ILoggerFactory?          loggerFactory,
            IEventSerializer?        eventSerializer,
            IMetadataSerializer?     metaSerializer,
            SqliteConnectionOptions? connectionOptions
        ) : base(options, checkpointStore, consumePipe, options.ConcurrencyLimit, kind, loggerFactory, eventSerializer, metaSerializer) {
        Schema = new(
            connectionOptions?.Schema is not null and not Schema.DefaultSchema
                ? connectionOptions.Schema
                : options.Schema
        );
        var connectionString = connectionOptions?.ConnectionString ?? options.ConnectionString;
        _connectionString = Ensure.NotEmptyString(connectionString);
        GetEndOfStream    = $"SELECT MAX(stream_position) FROM {Schema.MessagesTable}";
        GetEndOfAll       = $"SELECT MAX(global_position) FROM {Schema.MessagesTable}";
    }

    protected override async ValueTask<SqliteConnection> OpenConnection(CancellationToken cancellationToken)
        => await ConnectionFactory.GetConnection(_connectionString, cancellationToken).NoContext();

    protected override bool IsTransient(Exception exception) => false;

    protected override bool IsStopping(Exception exception)
        => exception is OperationCanceledException;

    protected override string GetEndOfStream { get; }
    protected override string GetEndOfAll    { get; }
}

public abstract record SqliteSubscriptionBaseOptions : SqlSubscriptionOptionsBase {
    protected SqliteSubscriptionBaseOptions() => Schema = Sqlite.Schema.DefaultSchema;
    public string? ConnectionString { get; set; }
}
