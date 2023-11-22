// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sql.Base.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.Postgresql.Subscriptions;

public abstract class PostgresSubscriptionBase<T>(
        NpgsqlDataSource dataSource,
        T                options,
        ICheckpointStore checkpointStore,
        ConsumePipe      consumePipe,
        ILoggerFactory?  loggerFactory
    ) : SqlSubscriptionBase<T, NpgsqlConnection>(options, checkpointStore, consumePipe, options.ConcurrencyLimit, loggerFactory)
    where T : PostgresSubscriptionBaseOptions {
    protected Schema           Schema     { get; } = new(options.Schema);
    protected NpgsqlDataSource DataSource { get; } = dataSource;

    protected override async ValueTask<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken)
        => await DataSource.OpenConnectionAsync(cancellationToken).NoContext();

    protected override bool IsTransient(Exception exception) => exception is PostgresException { IsTransient: true };
}

public abstract record PostgresSubscriptionBaseOptions : SqlSubscriptionOptionsBase;
