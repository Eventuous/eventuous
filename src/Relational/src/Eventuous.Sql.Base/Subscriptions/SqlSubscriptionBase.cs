// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data.Common;
using System.Text;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;

namespace Eventuous.Sql.Base.Subscriptions;

public abstract class SqlSubscriptionBase<TOptions, TConnection>(
        TOptions         options,
        ICheckpointStore checkpointStore,
        ConsumePipe      consumePipe,
        int              concurrencyLimit,
        ILoggerFactory?  loggerFactory
    )
    : EventSubscriptionWithCheckpoint<TOptions>(options, checkpointStore, consumePipe, concurrencyLimit, loggerFactory)
    where TOptions : SqlSubscriptionOptionsBase where TConnection : DbConnection {
    readonly IMetadataSerializer     _metaSerializer = DefaultMetadataSerializer.Instance;
    readonly CancellationTokenSource _cts            = new();

    protected abstract ValueTask<TConnection> OpenConnection(CancellationToken cancellationToken);

    protected abstract DbCommand PrepareCommand(TConnection connection, long start);

    protected abstract bool IsTransient(Exception exception);

    protected virtual bool IsStopping(Exception exception) => exception is OperationCanceledException;

    async Task PollingQuery(ulong? position, CancellationToken cancellationToken) {
        var start = position.HasValue ? (long)position : -1;

        var retryDelay = 10;

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

                retryDelay = 10;
            } catch (Exception e) {
                if (IsStopping(e)) {
                    IsDropped = true;
                    Log.InfoLog?.Log("Polling query stopped");

                    return;
                }

                if (IsTransient(e)) {
                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay *= 2;
                }
                else {
                    Log.InfoLog?.Log("Polling query stopped");
                    Dropped(DropReason.ServerError, e);

                    break;
                }
            }
        }
    }

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
        } catch (InvalidOperationException e) when (e.Message.Contains("Operation cancelled by user.")) {
            // It's a wrapped task cancelled exception
        }
    }

    protected virtual Task BeforeSubscribe(CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected abstract long MoveStart(PersistedEvent evt);

    protected IMessageConsumeContext ToConsumeContext(PersistedEvent evt, CancellationToken cancellationToken) {
        Logger.Current = Log;

        var data = DeserializeData(ContentType, evt.MessageType, Encoding.UTF8.GetBytes(evt.JsonData), evt.StreamName!, (ulong)evt.StreamPosition);

        var meta = evt.JsonMetadata == null
            ? new Metadata()
            : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return AsContext(evt, data, meta, cancellationToken);
    }

    protected abstract IMessageConsumeContext AsContext(PersistedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken);

    Task? _runner;

    protected const string ContentType = "application/json";
}
