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
        SubscriptionKind kind,
        ILoggerFactory?  loggerFactory
    )
    : EventSubscriptionWithCheckpoint<TOptions>(options, checkpointStore, consumePipe, concurrencyLimit, kind, loggerFactory)
    where TOptions : SqlSubscriptionOptionsBase where TConnection : DbConnection {
    readonly IMetadataSerializer _metaSerializer = DefaultMetadataSerializer.Instance;

    protected abstract ValueTask<TConnection> OpenConnection(CancellationToken cancellationToken);

    protected abstract DbCommand PrepareCommand(TConnection connection, long start);

    protected abstract bool IsTransient(Exception exception);

    protected virtual bool IsStopping(Exception exception) => exception is OperationCanceledException;

    // ReSharper disable once CognitiveComplexity

    async Task PollingQuery(ulong? position, CancellationToken cancellationToken) {
        var start = position.HasValue ? (long)position : -1;

        var retryCount   = 0;
        var currentDelay = Options.Polling.MinIntervalMs;

        try {
            await ExecutePollCycle();
        } finally {
            Log.InfoLog?.Log("Polling query stopped");
        }

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

        async Task ExecutePollCycle() {
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
        }
    }

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        await BeforeSubscribe(cancellationToken).NoContext();
        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        _runner = new TaskRunner(token => PollingQuery(position, token)).Start();
    }

    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        if (_runner == null) return;

        await _runner.Stop(cancellationToken);
        _runner.Dispose();
        _runner = null;
    }

    protected virtual Task BeforeSubscribe(CancellationToken cancellationToken) => Task.CompletedTask;

#pragma warning disable CS8524
    long MoveStart(PersistedEvent evt) => Kind switch {
#pragma warning restore CS8524
        SubscriptionKind.All    => evt.GlobalPosition,
        SubscriptionKind.Stream => evt.StreamPosition,
    };

    MessageConsumeContext ToConsumeContext(PersistedEvent evt, CancellationToken cancellationToken) {
        Logger.Current = Log;

        var data = DeserializeData(ContentType, evt.MessageType, Encoding.UTF8.GetBytes(evt.JsonData), evt.StreamName!, (ulong)evt.StreamPosition);

        var meta = evt.JsonMetadata == null
            ? new Metadata()
            : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return AsContext(evt, data, meta, cancellationToken);
    }

    MessageConsumeContext AsContext(PersistedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken)
#pragma warning disable CS8524
        => Kind switch {
#pragma warning restore CS8524
            SubscriptionKind.Stream => new MessageConsumeContext(
                evt.MessageId.ToString(),
                evt.MessageType,
                ContentType,
                evt.StreamName!,
                (ulong)evt.StreamPosition,
                (ulong)evt.StreamPosition,
                (ulong)evt.GlobalPosition,
                Sequence++,
                evt.Created,
                e,
                meta,
                Options.SubscriptionId,
                cancellationToken
            ),
            SubscriptionKind.All => new MessageConsumeContext(
                evt.MessageId.ToString(),
                evt.MessageType,
                ContentType,
                Ensure.NotEmptyString(evt.StreamName),
                (ulong)evt.StreamPosition,
                (ulong)evt.StreamPosition,
                (ulong)evt.GlobalPosition,
                Sequence++,
                evt.Created,
                e,
                meta,
                Options.SubscriptionId,
                cancellationToken
            )
        };

    TaskRunner? _runner;

    const string ContentType = "application/json";

    record struct PollingResult(bool Continue, bool Retry, int ReceivedEvents);
}
