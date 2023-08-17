// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions;

using Extensions;

public abstract class SqlServerSubscriptionBase<T>(
        GetSqlServerConnection getConnection,
        T                      options,
        ICheckpointStore       checkpointStore,
        ConsumePipe            consumePipe,
        ILoggerFactory?        loggerFactory
    )
    : EventSubscriptionWithCheckpoint<T>(options, checkpointStore, consumePipe, options.ConcurrencyLimit, loggerFactory)
    where T : SqlServerSubscriptionBaseOptions {
    readonly IMetadataSerializer     _metaSerializer = DefaultMetadataSerializer.Instance;
    readonly CancellationTokenSource _cts            = new();

    protected Schema                 Schema        { get; } = new(options.Schema);
    protected GetSqlServerConnection GetConnection { get; } = Ensure.NotNull<GetSqlServerConnection>(getConnection, "Connection factory");

    async Task<SqlConnection> OpenConnection(CancellationToken cancellationToken) {
        var connection = GetConnection();
        await connection.OpenAsync(cancellationToken).NoContext();

        return connection;
    }

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        await BeforeSubscribe(cancellationToken).NoContext();

        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        _runner = Task.Run(() => PollingQuery(position + 1, _cts.Token), _cts.Token);
    }

    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        try {
            _cts.Cancel();
            if (_runner != null) await _runner.NoContext();
        } catch (OperationCanceledException) {
            // Nothing to do
        }
    }

    protected const string ContentType = "application/json";

    Task? _runner;

    // ReSharper disable once StaticMemberInGenericType
    static readonly List<int> TransientErrorNumbers = new() { 4060, 40197, 40501, 40613, 49918, 49919, 49920, 11001 };

    async Task PollingQuery(ulong? position, CancellationToken cancellationToken) {
        var start = position.HasValue ? (long)position : -1;

        while (!cancellationToken.IsCancellationRequested) {
            try {
                await using var connection = await OpenConnection(cancellationToken).NoContext();
                await using var cmd        = PrepareCommand(connection, start);
                await using var reader     = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

                var result = reader.ReadEvents(cancellationToken);

                await foreach (var persistedEvent in result.WithCancellation(cancellationToken).ConfigureAwait(false)) {
                    await HandleInternal(ToConsumeContext(persistedEvent, cancellationToken)).NoContext();
                    start = MoveStart(persistedEvent);
                }
            } catch (OperationCanceledException) {
                // Nothing to do
            } catch (SqlException e) when (TransientErrorNumbers.Contains(e.Number)) {
                // Try again
            } catch (Exception e) {
                IsDropped = true;
                Log.WarnLog?.Log(e, "Dropped");

                throw;
            }
        }
    }

    protected abstract SqlCommand PrepareCommand(SqlConnection connection, long start);

    protected virtual Task BeforeSubscribe(CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected abstract long MoveStart(PersistedEvent evt);

    IMessageConsumeContext ToConsumeContext(PersistedEvent evt, CancellationToken cancellationToken) {
        var data = DeserializeData(ContentType, evt.MessageType, Encoding.UTF8.GetBytes(evt.JsonData), evt.StreamName!, (ulong)evt.StreamPosition);

        var meta = evt.JsonMetadata == null
            ? new Metadata()
            : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return AsContext(evt, data, meta, cancellationToken);
    }

    protected abstract IMessageConsumeContext AsContext(PersistedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken);
}

public abstract record SqlServerSubscriptionBaseOptions : SubscriptionWithCheckpointOptions {
    public string Schema           { get; set; } = "eventuous";
    public int    ConcurrencyLimit { get; set; } = 1;
    public int    MaxPageSize      { get; set; } = 1024;
}
