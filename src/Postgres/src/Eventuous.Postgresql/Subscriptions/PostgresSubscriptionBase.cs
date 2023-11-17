// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;

namespace Eventuous.Postgresql.Subscriptions;

using Extensions;

public abstract class PostgresSubscriptionBase<T>(
        NpgsqlDataSource dataSource,
        T                options,
        ICheckpointStore checkpointStore,
        ConsumePipe      consumePipe,
        ILoggerFactory?  loggerFactory
    ) : EventSubscriptionWithCheckpoint<T>(options, checkpointStore, consumePipe, options.ConcurrencyLimit, loggerFactory)
    where T : PostgresSubscriptionBaseOptions {
    readonly  IMetadataSerializer     _metaSerializer = DefaultMetadataSerializer.Instance;
    readonly  CancellationTokenSource _cts            = new();
    protected Schema                  Schema     { get; } = new(options.Schema);
    protected NpgsqlDataSource        DataSource { get; } = dataSource;

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        await BeforeSubscribe(cancellationToken).NoContext();

        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        _runner = Task.Run(() => PollingQuery(position, _cts.Token), _cts.Token);
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

    async Task PollingQuery(ulong? position, CancellationToken cancellationToken) {
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

    protected virtual Task BeforeSubscribe(CancellationToken cancellationToken) => Task.CompletedTask;

    protected abstract long MoveStart(PersistedEvent evt);

    IMessageConsumeContext ToConsumeContext(PersistedEvent evt, CancellationToken cancellationToken) {
        Logger.Current = Log;

        var data = DeserializeData(ContentType, evt.MessageType, Encoding.UTF8.GetBytes(evt.JsonData), evt.StreamName!, (ulong)evt.StreamPosition);

        var meta = evt.JsonMetadata == null ? new Metadata() : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return AsContext(evt, data, meta, cancellationToken);
    }

    protected abstract IMessageConsumeContext AsContext(PersistedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken);
}

public abstract record PostgresSubscriptionBaseOptions : SubscriptionWithCheckpointOptions {
    public string Schema           { get; set; } = "eventuous";
    public int    ConcurrencyLimit { get; set; } = 1;
    public int    MaxPageSize      { get; set; } = 1024;
}
