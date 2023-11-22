// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sql.Base;
using Eventuous.Sql.Base.Subscriptions;
using Eventuous.Subscriptions;
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
    ) : SqlSubscriptionBase<T>(options, checkpointStore, consumePipe, options.ConcurrencyLimit, loggerFactory)
    where T : PostgresSubscriptionBaseOptions {
    protected Schema                  Schema     { get; } = new(options.Schema);
    protected NpgsqlDataSource        DataSource { get; } = dataSource;

    protected override async Task PollingQuery(ulong? position, CancellationToken cancellationToken) {
        var start = position.HasValue ? (long)position : -1;

        var retryDelay = 10;

        while (!cancellationToken.IsCancellationRequested) {
            try {
                await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
                await using var cmd        = PrepareCommand(connection, start);
                await using var reader     = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

                var result = reader.ReadEvents(cancellationToken);

                await foreach (var persistedEvent in result.NoContext(cancellationToken)) {
                    await HandleInternal(ToConsumeContext(persistedEvent, cancellationToken)).NoContext();
                    start = MoveStart(persistedEvent);
                }

                retryDelay = 10;
            } catch (OperationCanceledException) {
                // Nothing to do
            } catch (PostgresException e) when (e.IsTransient) {
                await Task.Delay(retryDelay, cancellationToken);
                retryDelay *= 2;
            } catch (Exception e) {
                Dropped(DropReason.ServerError, e);

                break;
            }
        }
    }

    protected abstract NpgsqlCommand PrepareCommand(NpgsqlConnection connection, long start);
}

public abstract record PostgresSubscriptionBaseOptions : SqlSubscriptionOptionsBase;
