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

    // ReSharper disable once CognitiveComplexity
    async Task PollingQuery(ulong? position, CancellationToken cancellationToken) {
        var start = position.HasValue ? (long)position : -1;

        var retryCount   = 0;
        var currentDelay = Options.Polling.MinIntervalMs;

        while (!cancellationToken.IsCancellationRequested) {
            var result = await Poll().NoContext();

            if (!result.Continue) break;

            if (result.Retry) {
                await Task.Delay(Options.Retry.InitialDelayMs * retryCount++, cancellationToken).NoContext();

                continue;
            }

            retryCount = 0;

            // Poll again immediately if we received events
            if (result.ReceivedEvents > 0) {
                currentDelay = Options.Polling.MinIntervalMs;

                continue;
            }

            // Otherwise, wait a bit
            // Exponentially increase delay but do not exceed maxDelay
            currentDelay = Math.Min((int)(currentDelay * Options.Polling.GrowFactor), Options.Polling.MaxIntervalMs);
            await Task.Delay(currentDelay, cancellationToken).NoContext();
        }

        Log.InfoLog?.Log("Polling query stopped");

        return;

        async Task<PollingResult> Poll() {
            try {
                await using var connection = await OpenConnection(cancellationToken).NoContext();
                await using var cmd        = PrepareCommand(connection, start);
                await using var reader     = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

                var result = reader.ReadEvents(cancellationToken);

                var received = 0;

                await foreach (var persistedEvent in result.NoContext(cancellationToken)) {
                    await HandleInternal(ToConsumeContext(persistedEvent, cancellationToken)).NoContext();
                    start = MoveStart(persistedEvent);
                    received++;
                }

                return new PollingResult(true, false, received);
            } catch (Exception e) {
                if (IsStopping(e)) {
                    IsDropped = true;

                    return new PollingResult(false, false, 0);
                }

                if (IsTransient(e)) {
                    return new PollingResult(true, true, 0);
                }

                Dropped(DropReason.ServerError, e);

                return new PollingResult(false, false, 0);
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

    protected virtual Task BeforeSubscribe(CancellationToken cancellationToken) => Task.CompletedTask;

    protected abstract long MoveStart(PersistedEvent evt);

    IMessageConsumeContext ToConsumeContext(PersistedEvent evt, CancellationToken cancellationToken) {
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

    record struct PollingResult(bool Continue, bool Retry, int ReceivedEvents);
}
