// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

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

    private record DetectedGap(long Position, DateTime FirstSeen);

    /// <summary>
    /// The polling loop. Its only clean exit is a stop request; every other exit is a fault the pump in
    /// <see cref="Connect"/> reads as a drop.
    /// </summary>
    async Task Poll(SubscriptionRun run, long start, CancellationToken cancellationToken) {
        DetectedGap? gap = null;

        var retryCount   = 0;
        var currentDelay = Options.Polling.MinIntervalMs;

        try {
            await ExecutePollCycle();
        } finally {
            Log.InfoLog?.Log("Polling query stopped");
        }

        return;

        async Task<PollingResult> PollOnce() {
            try {
                await using var connection = await OpenConnection(cancellationToken).NoContext();
                await using var cmd        = PrepareCommand(connection, start);
                await using var reader     = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

                var result = reader.ReadEvents(cancellationToken);

                var received = 0;

                await foreach (var persistedEvent in result.NoContext(cancellationToken)) {
                    // For All subscriptions we need to ensure we don't skip not-yet-committed events from other concurrent transactions.
                    // If we observe a gap in the global position sequence, we stop processing further events in this poll cycle
                    // and will retry on the next poll. This prevents advancing the checkpoint beyond an uncommitted (invisible) row.
                    if (Kind == SubscriptionKind.All) {
                        gap = DetectGap(start, persistedEvent, gap);

                        if (gap != null)
                            break;
                    }

                    if (!ShouldSkipEvent(persistedEvent)) {
                        await HandleInternal(run, ToConsumeContext(run, persistedEvent, cancellationToken)).NoContext();
                    }

                    start = MoveStart(persistedEvent);
                    received++;
                }


                // If a gap persists beyond timeout, attempt provider-specific remediation (e.g. tombstone insert).
                if (Kind == SubscriptionKind.All && gap != null && Options.GapHandlingTimeoutMs != null) {
                    var gapAge = DateTime.UtcNow - gap.FirstSeen;

                    if (gapAge.TotalMilliseconds >= Options.GapHandlingTimeoutMs.Value) {
                        await HandleGapTimeout(gap.Position, start, cancellationToken).NoContext();
                    }
                }

                return new(true, gap != null, received);
            } catch (Exception e) {
                // IsStopping alone isn't enough: providers can report unrelated aborts (e.g. SQL Server's
                // "Operation cancelled by user.") with the same shape, so only trust it once the token agrees.
                if (IsStopping(e) && cancellationToken.IsCancellationRequested) return new(false, false, 0);

                if (IsTransient(e)) {
                    return new(true, true, 0);
                }

                // Let it propagate instead of reporting here too — a faulted pump is already a drop.
                throw;
            }
        }

        async Task ExecutePollCycle() {
            while (!cancellationToken.IsCancellationRequested) {
                var result = await PollOnce().NoContext();

                if (!result.Continue) return;

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

    DetectedGap? DetectGap(long start, PersistedEvent persistedEvent, DetectedGap? previousGap) {
        var expectedNext = start < 0 ? 1 : start + 1; // global position identity starts at 1

        if (persistedEvent.GlobalPosition > expectedNext) {
            if (previousGap != null) {
                if (Options.GapSkipTimeoutMs == null || (DateTime.UtcNow - previousGap.FirstSeen) < TimeSpan.FromMilliseconds(Options.GapSkipTimeoutMs.Value)) {
                    return previousGap;
                }
            }

            var newGapAge = DateTime.UtcNow - persistedEvent.Created;

            if (Options.GapAgeThresholdMs == null || newGapAge.TotalMilliseconds < Options.GapAgeThresholdMs.Value) {
                return new(expectedNext, DateTime.UtcNow);
            }
        }

        return null;
    }

    /// <summary>
    /// Starts the subscription
    /// </summary>
    /// <param name="run">The run being connected; reassigned every call, since a run is repeatable on this instance.</param>
    protected override async ValueTask Connect(SubscriptionRun run) {
        await BeforeSubscribe(run.Token).NoContext();
        var checkpoint = await GetCheckpoint(run).NoContext();
        var position   = checkpoint.Position;

        if (position == null && Options.StartFrom == InitialPosition.Latest) {
            var endOfStream = await GetSubscriptionEndOfStream(run.Token).NoContext();
            if (endOfStream == EndOfStream.Invalid) {
                throw new InvalidOperationException($"Could not get the end of the stream for subscription {SubscriptionId}");
            }
            await CheckpointStore.StoreCheckpoint(new(SubscriptionId, endOfStream.Position), true, run.Token).NoContext();
            position = endOfStream.Position;
        }

        // Local rather than a field: a later Connect on this instance must not move the position under a
        // loop that is still winding down.
        var start = position.HasValue ? (long)position : -1;

        // Runs on its own task so Connect never blocks; classified here because nothing else observes this task.
        var pumping = Task.Run(
            async () => {
                try {
                    await Poll(run, start, run.Token).NoContext();
                } catch (Exception) when (run.Token.IsCancellationRequested) {
                    // This run's own token asked for it: graceful, not a drop.
                } catch (Exception e) {
                    // Any other cancellation (an inner deadline, a WaitAsync timeout) is a real drop cause.
                    run.Fail(DropReason.ServerError, e);
                }
            },
            CancellationToken.None
        );

        // No handle of its own to release: registered purely to join the loop before the next Connect starts.
        run.OnDisconnect(_ => new(pumping));
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

    MessageConsumeContext ToConsumeContext(SubscriptionRun run, PersistedEvent evt, CancellationToken cancellationToken) {
        Logger.Current = Log;

        var data = DeserializeData(ContentType, evt.MessageType, Encoding.UTF8.GetBytes(evt.JsonData), evt.StreamName!, (ulong)evt.StreamPosition);

        var meta = evt.JsonMetadata == null ? new() : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return AsContext(run, evt, data, meta, cancellationToken);
    }

    MessageConsumeContext AsContext(SubscriptionRun run, PersistedEvent evt, object? e, Metadata? meta, CancellationToken cancellationToken)
        => Kind switch {
            SubscriptionKind.Stream => new(
                evt.MessageId.ToString(),
                evt.MessageType,
                ContentType,
                evt.StreamName!,
                (ulong)evt.StreamPosition,
                (ulong)evt.StreamPosition,
                (ulong)evt.GlobalPosition,
                run.NextSequence(),
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
                run.NextSequence(),
                evt.Created,
                e,
                meta,
                Options.SubscriptionId,
                cancellationToken
            )
        };

    const string ContentType = "application/json";

    /// <summary>
    /// Provider-specific hook: attempt to resolve a persistent gap (e.g. insert tombstone rows).
    /// Base implementation does nothing. Implementations should be idempotent; this method may be called repeatedly
    /// until the gap is naturally filled by the original row becoming visible or by a remedial action (like tombstone insertion).
    /// </summary>
    /// <param name="gapPosition">The missing global position</param>
    /// <param name="currentStart">Current start pointer</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual ValueTask HandleGapTimeout(long gapPosition, long currentStart, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>
    /// Determine if the event should be skipped instead of dispatched to user handlers.
    /// Base implementation returns false; providers can override.
    /// </summary>
    /// <param name="evt"></param>
    /// <returns></returns>
    protected virtual bool ShouldSkipEvent(PersistedEvent evt) => false;

    [StructLayout(LayoutKind.Auto)]
    readonly record struct PollingResult(bool Continue, bool Retry, int ReceivedEvents);

    GetSubscriptionEndOfStream IMeasuredSubscription.GetMeasure() => GetSubscriptionEndOfStream;

    /// <summary>
    /// Prepares a command that returns the position the subscription gap is measured against: the last global
    /// position for an <see cref="SubscriptionKind.All"/> subscription, or the last position within the subscribed
    /// stream for a <see cref="SubscriptionKind.Stream"/> one. The position is read from the first column of the
    /// first row and may be <c>NULL</c> when there is nothing to measure yet.
    /// </summary>
    /// <param name="connection">Connection that can be used to create the command</param>
    /// <remarks>
    /// The measure is also handed to the metrics observer, which can invoke it before the subscription has ever
    /// connected, so the command must not depend on state resolved in <see cref="BeforeSubscribe"/>.
    /// </remarks>
    protected abstract DbCommand PrepareEndOfStreamCommand(TConnection connection);

    async ValueTask<EndOfStream> GetSubscriptionEndOfStream(CancellationToken cancellationToken) {
        try {
            await using var connection = await OpenConnection(cancellationToken).NoContext();
            await using var cmd        = PrepareEndOfStreamCommand(connection);
            await using var reader     = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

            // MAX(...) returns NULL on an empty table, and providers may return the position column as either
            // Int32 or Int64, so guard against DBNull and convert rather than calling the strict GetInt64.
            var position = await reader.ReadAsync(cancellationToken).NoContext() && reader[0] is not DBNull
                ? Convert.ToInt64(reader[0])
                : 0;

            return new(SubscriptionId, (ulong)position, DateTime.UtcNow);
        } catch (Exception e) {
            Log.WarnLog?.Log(e, "Failed to get end of stream");

            return EndOfStream.Invalid;
        }
    }
}
