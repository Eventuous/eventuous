// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sql.Base;
using Eventuous.Sql.Base.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions;

public abstract class SqlServerSubscriptionBase<T>(
        GetSqlServerConnection getConnection,
        T                      options,
        ICheckpointStore       checkpointStore,
        ConsumePipe            consumePipe,
        ILoggerFactory?        loggerFactory
    )
    : SqlSubscriptionBase<T>(options, checkpointStore, consumePipe, options.ConcurrencyLimit, loggerFactory)
    where T : SqlServerSubscriptionBaseOptions {
    protected Schema                 Schema        { get; } = new(options.Schema);
    protected GetSqlServerConnection GetConnection { get; } = Ensure.NotNull<GetSqlServerConnection>(getConnection, "Connection factory");

    async Task<SqlConnection> OpenConnection(CancellationToken cancellationToken) {
        var connection = GetConnection();
        await connection.OpenAsync(cancellationToken).NoContext();

        return connection;
    }

    // ReSharper disable once StaticMemberInGenericType
    static readonly List<int> TransientErrorNumbers = [4060, 40197, 40501, 40613, 49918, 49919, 49920, 11001];

    protected override async Task PollingQuery(ulong? position, CancellationToken cancellationToken) {
        var start = position.HasValue ? (long)position : -1;

        while (!cancellationToken.IsCancellationRequested) {
            try {
                await using var connection = await OpenConnection(cancellationToken).NoContext();
                await using var cmd        = PrepareCommand(connection, start);
                await using var reader     = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

                var result = reader.ReadEvents(cancellationToken);

                await foreach (var persistedEvent in result.NoContext(cancellationToken)) {
                    await HandleInternal(ToConsumeContext(persistedEvent, cancellationToken)).NoContext();
                    start = MoveStart(persistedEvent);
                }
            } catch (OperationCanceledException) {
                ReportStop();
            } catch (SqlException e) when (TransientErrorNumbers.Contains(e.Number)) {
                // Try again
            } catch (SqlException e) when (e.Message.Contains("Operation cancelled by user.")) {
                ReportStop();
            } catch (SqlException e) when (e.Number == 3980 && e.Message.Contains("Operation cancelled by user.")) {
                ReportStop();
            } catch (Exception e) {
                ReportStop();
                Log.WarnLog?.Log(e, "Dropped");

                throw;
            }
        }

        return;

        void ReportStop() {
            IsDropped = true;
            Log.InfoLog?.Log("Polling query stopped");
        }
    }

    protected abstract SqlCommand PrepareCommand(SqlConnection connection, long start);
}

public abstract record SqlServerSubscriptionBaseOptions : SqlSubscriptionOptionsBase;
