// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using System.Data.Common;
using System.Runtime.InteropServices;
using System.Text;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;

namespace Eventuous.Sql.Base.Subscriptions;

/// <summary>
/// Base class for subscriptions that use relational databases and ADO.NET
/// </summary>
/// <param name="options">Subscription options</param>
/// <param name="checkpointStore">Checkpoint store for the subscription</param>
/// <param name="consumePipe">Pre-populated consume pipe</param>
/// <param name="concurrencyLimit">Limit the number of concurrent consumers</param>
/// <param name="kind">All or Stream</param>
/// <param name="loggerFactory">Logger factory (optional)</param>
/// <param name="eventSerializer">Event serializer (optional)</param>
/// <param name="metaSerializer">Metadata serializer (optional)</param>
/// <typeparam name="TOptions">Subscription options type</typeparam>
/// <typeparam name="TConnection"></typeparam>
public abstract class SqlSubscriptionBase<TOptions, TConnection>(
        TOptions             options,
        ICheckpointStore     checkpointStore,
        ConsumePipe          consumePipe,
        int                  concurrencyLimit,
        SubscriptionKind     kind,
        ILoggerFactory?      loggerFactory,
        IEventSerializer?    eventSerializer,
        IMetadataSerializer? metaSerializer
    )
    : EventSubscriptionWithCheckpoint<TOptions>(options, checkpointStore, consumePipe, concurrencyLimit, kind, loggerFactory, eventSerializer, metaSerializer),
        IMeasuredSubscription
    where TOptions : SqlSubscriptionOptionsBase where TConnection : DbConnection {
    readonly IMetadataSerializer _metaSerializer = DefaultMetadataSerializer.Instance;

    /// <summary>
    /// Create and open the SQL connection
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected abstract ValueTask<TConnection> OpenConnection(CancellationToken cancellationToken);

    /// <summary>
    /// Prepares a command to poll the messages table for new records
    /// </summary>
    /// <param name="connection">Connection that can be used to create the command</param>
    /// <param name="start">Starting position</param>
    /// <returns></returns>
    protected abstract DbCommand PrepareCommand(TConnection connection, long start);

    /// <summary>
    /// Returns true if the SQL operation returned a transient exception
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    protected abstract bool IsTransient(Exception exception);

    /// <summary>
    /// Returns true if the subscription is stopping
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
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

                    return new(false, false, 0);
                }

                if (IsTransient(e)) {
                    return new(true, true, 0);
                }

                Dropped(DropReason.ServerError, e);

                return new(false, false, 0);
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

    /// <summary>
    /// Starts the subscription
    /// </summary>
    /// <param name="cancellationToken"></param>
    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        await BeforeSubscribe(cancellationToken).NoContext();
        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        _runner = new TaskRunner(token => PollingQuery(position, token)).Start();
    }

    /// <summary>
    /// Stops the subscription.
    /// </summary>
    /// <param name="cancellationToken"></param>
    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        if (_runner == null) return;

        await _runner.Stop(cancellationToken);
        _runner.Dispose();
        _runner = null;
    }

    /// <summary>
    /// This function is called before the subscription starts.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual Task BeforeSubscribe(CancellationToken cancellationToken) => Task.CompletedTask;

    long MoveStart(PersistedEvent evt) => Kind switch {
        SubscriptionKind.All    => evt.GlobalPosition,
        SubscriptionKind.Stream => evt.StreamPosition
    };

    MessageConsumeContext ToConsumeContext(PersistedEvent evt, CancellationToken cancellationToken) {
        Logger.Current = Log;

        var data = DeserializeData(ContentType, evt.MessageType, Encoding.UTF8.GetBytes(evt.JsonData), evt.StreamName!, (ulong)evt.StreamPosition);

        var meta = evt.JsonMetadata == null ? new() : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return AsContext(evt, data, meta, cancellationToken);
    }

    MessageConsumeContext AsContext(PersistedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken)
        => Kind switch {
            SubscriptionKind.Stream => new(
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
            SubscriptionKind.All => new(
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

    [StructLayout(LayoutKind.Auto)]
    readonly record struct PollingResult(bool Continue, bool Retry, int ReceivedEvents);

    GetSubscriptionEndOfStream IMeasuredSubscription.GetMeasure() => GetSubscriptionEndOfStream;

    /// <summary>
    /// Get SQL statement to get the end of the stream
    /// </summary>
    protected abstract string GetEndOfStream { get; }

    /// <summary>
    /// Get SQL statement to get the end of the global log
    /// </summary>
    protected abstract string GetEndOfAll { get; }

    async ValueTask<EndOfStream> GetSubscriptionEndOfStream(CancellationToken cancellationToken) {
        try {
            await using var connection = await OpenConnection(cancellationToken).NoContext();
            await using var cmd        = connection.CreateCommand();
            cmd.CommandType = CommandType.Text;

            cmd.CommandText = Kind switch {
                SubscriptionKind.All    => GetEndOfStream,
                SubscriptionKind.Stream => GetEndOfAll
            };
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

            var position = await reader.ReadAsync(cancellationToken).NoContext() ? reader.GetInt64(0) : 0;

            return new(SubscriptionId, (ulong)position, DateTime.UtcNow);
        } catch (Exception) {
            Log.WarnLog?.Log("Failed to get end of stream");

            return EndOfStream.Invalid;
        }
    }
}
