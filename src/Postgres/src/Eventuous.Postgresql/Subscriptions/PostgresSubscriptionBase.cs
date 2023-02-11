// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Text;
using Eventuous.Postgresql.Extensions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Eventuous.Tools;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Eventuous.Postgresql.Subscriptions;

public abstract class PostgresSubscriptionBase<T> : EventSubscriptionWithCheckpoint<T>
    where T : PostgresSubscriptionBaseOptions {
    readonly           IMetadataSerializer     _metaSerializer;
    readonly           CancellationTokenSource _cts = new();
    protected readonly NpgsqlDataSource        DataSource;
    protected          Schema                  Schema { get; }

    protected PostgresSubscriptionBase(
        NpgsqlDataSource dataSource,
        T                options,
        ICheckpointStore checkpointStore,
        ConsumePipe      consumePipe,
        ILoggerFactory?  loggerFactory
    ) : base(options, checkpointStore, consumePipe, options.ConcurrencyLimit, loggerFactory) {
        Schema          = new Schema(options.Schema);
        DataSource      = dataSource;
        _metaSerializer = DefaultMetadataSerializer.Instance;
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
        }
        catch (OperationCanceledException) {
            // Nothing to do
        }
    }

    protected const string ContentType = "application/json";

    Task? _runner;

    async Task PollingQuery(ulong? position, CancellationToken cancellationToken) {
        var start = position.HasValue
            ? (long) position
            : -1;

        while (!cancellationToken.IsCancellationRequested) {
            try {
                await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
                await using var cmd = PrepareCommand(connection, start);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

                var result = reader.ReadEvents(cancellationToken);
                await foreach (var persistedEvent in result.WithCancellation(cancellationToken).ConfigureAwait(false)) {
                    await HandleInternal(ToConsumeContext(persistedEvent, cancellationToken)).NoContext();
                    start = MoveStart(persistedEvent);
                }
            }
            catch (OperationCanceledException) {
                // Nothing to do
            }
            catch (PostgresException e) when (e.IsTransient) {
                // Try again
            }
            catch (Exception e) {
                IsDropped = true;
                Log.WarnLog?.Log(e, "Dropped");
                throw;
            }
        }
    }

    protected abstract NpgsqlCommand PrepareCommand(NpgsqlConnection connection, long start);

    protected virtual Task BeforeSubscribe(CancellationToken cancellationToken) => Task.CompletedTask;

    protected abstract long MoveStart(PersistedEvent evt);

    IMessageConsumeContext ToConsumeContext(PersistedEvent evt, CancellationToken cancellationToken) {
        Logger.Current = Log;
        var data = DeserializeData(
            ContentType,
            evt.MessageType,
            Encoding.UTF8.GetBytes(evt.JsonData),
            evt.StreamName!,
            (ulong) evt.StreamPosition
        );

        var meta = evt.JsonMetadata == null
            ? new Metadata()
            : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return AsContext(evt, data, meta, cancellationToken);
    }

    protected abstract IMessageConsumeContext AsContext(
        PersistedEvent    evt,
        object?           e,
        Metadata?         meta,
        CancellationToken cancellationToken
    );
}

public abstract record PostgresSubscriptionBaseOptions : SubscriptionOptions {
    public string Schema           { get; set; } = "eventuous";
    public int    ConcurrencyLimit { get; set; } = 1;
    public int    MaxPageSize      { get; set; } = 1024;
}