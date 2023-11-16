// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.Checkpoints;

using Channels;
using Logging;
using static Diagnostics.SubscriptionsEventSource;

public sealed class CheckpointCommitHandler : IAsyncDisposable {
    readonly ILoggerFactory?               _loggerFactory;
    readonly string                        _subscriptionId;
    readonly CommitCheckpoint              _commitCheckpoint;
    readonly CommitPositionSequence        _positions = new();
    readonly ChannelWorker<CommitPosition> _worker;

    CommitPosition _lastCommit = CommitPosition.None;

    public const string DiagnosticName  = "eventuous.checkpoint.commithandler";
    public const string CommitOperation = "Commit";

    static readonly DiagnosticSource Diagnostic = new DiagnosticListener(DiagnosticName);

    readonly Subject<CommitPosition> _subject;

    internal record CommitEvent(string Id, CommitPosition CommitPosition, CommitPosition? FirstPending);

    public CheckpointCommitHandler(
            string           subscriptionId,
            ICheckpointStore checkpointStore,
            TimeSpan         delay,
            int              batchSize     = 1,
            ILoggerFactory?  loggerFactory = null
        )
        : this(subscriptionId, checkpointStore.StoreCheckpoint, delay, batchSize, loggerFactory) { }

    public CheckpointCommitHandler(
            string           subscriptionId,
            CommitCheckpoint commitCheckpoint,
            TimeSpan         delay,
            int              batchSize     = 1,
            ILoggerFactory?  loggerFactory = null
        ) {
        _subscriptionId   = subscriptionId;
        _commitCheckpoint = commitCheckpoint;
        _loggerFactory    = loggerFactory;
        var channel = Channel.CreateBounded<CommitPosition>(batchSize * 1000);
        _subject = new Subject<CommitPosition>();

        _subject
            .Buffer(delay, batchSize)
            .Where(x => x.Count > 0)
            .Select(AddBatchAndGetLast)
            .Where(x => x.Valid)
            .Select(x => Observable.FromAsync(ct => CommitInternal(x, false, ct)))
            .Concat()
            .Subscribe(
                static _ => { },
                exception => {
                    Logger.ConfigureIfNull(_subscriptionId, _loggerFactory);
                    Logger.Current.ErrorLog?.Log(exception, "Commit handler error");
                },
                () => {
                    if (_lastCommit.Valid)
                        CommitInternal(_lastCommit, true, default).NoContext().GetAwaiter().GetResult();
                }
            );

        _worker = new ChannelWorker<CommitPosition>(channel, Process, true);

        _worker.OnDispose = _ => {
            _subject.OnCompleted();
            _subject.Dispose();

            return default;
        };

        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValueTask Process(CommitPosition position, CancellationToken cancellationToken) {
            position.LogContext.PositionReceived(position);
            _subject.OnNext(position);

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        CommitPosition AddBatchAndGetLast(IList<CommitPosition> list) {
            _lock.EnterWriteLock();
            _positions.UnionWith(list);
            _lock.ExitWriteLock();

            var next = GetCommitPosition(false);

            return next;
        }
    }

    /// <summary>
    /// Commit a position to be stored, the store action can be delayed
    /// </summary>
    /// <param name="position">Position to commit</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    [PublicAPI]
    public ValueTask Commit(CommitPosition position, CancellationToken cancellationToken) {
        if (Diagnostic.IsEnabled(CommitOperation)) Diagnostic.Write(CommitOperation, new CommitEvent(_subscriptionId, position, _positions.Min));

        return _worker.Write(position, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    CommitPosition GetCommitPosition(bool force) {
        _lock.EnterReadLock();
        var pos = _lastCommit.Valid switch {
            // There's a gap between the last committed position and the list head
            true when _lastCommit.Sequence + 1 != _positions.Min.Sequence && !force => AtGap(),
            // The list head is not at the very beginning
            false when _positions.Min.Sequence != 0                       => WrongHead(),
            true when _lastCommit.Sequence     == _positions.Min.Sequence => BeforeGap(),
            _                                                             => _positions.FirstBeforeGap()
        };
        _lock.ExitReadLock();

        return pos;

        CommitPosition AtGap() {
            Log.CheckpointLastCommitGap(_lastCommit, _positions.Min);

            return CommitPosition.None;
        }

        CommitPosition WrongHead() {
            Log.CheckpointSequenceInvalidHead(_positions.Min);

            return CommitPosition.None;
        }

        CommitPosition BeforeGap() {
            Log.CheckpointLastCommitDuplicate(_positions.Min);

            return _positions.FirstBeforeGap();
        }
    }

    readonly ReaderWriterLockSlim _lock = new();

    async Task CommitInternal(CommitPosition position, bool force, CancellationToken cancellationToken) {
        _lock.EnterWriteLock();

        try {
            if (_lastCommit == position && !force) {
                Log.CheckpointAlreadyCommitted(_subscriptionId, position);

                return;
            }

            position.LogContext.CommittingPosition(position);
            await _commitCheckpoint(new Checkpoint(_subscriptionId, position.Position), force, cancellationToken).NoContext();
            _lastCommit = position;
            _positions.RemoveWhere(x => x.Sequence <= position.Sequence);
        } catch (OperationCanceledException) {
            await _commitCheckpoint(new Checkpoint(_subscriptionId, position.Position), true, default).NoContext();
            _positions.RemoveWhere(x => x.Sequence <= position.Sequence);
        } catch (Exception e) {
            position.LogContext.UnableToCommitPosition(position, e);
        } finally {
            _lock.ExitWriteLock();
        }
    }

    public async ValueTask DisposeAsync() {
        Logger.ConfigureIfNull(_subscriptionId, _loggerFactory);
        Logger.Current.InfoLog?.Log("Stopping commit handler worker");

        await _worker.DisposeAsync().NoContext();
        _positions.Clear();
    }
}

public readonly record struct CommitPosition(ulong Position, ulong Sequence, DateTime Timestamp) {
    public bool Valid { get; private init; } = true;

    public LogContext LogContext { get; init; }

    public static readonly CommitPosition None = new(0, 0, DateTime.MinValue) { Valid = false };

    public bool Equals(CommitPosition other) => Valid == other.Valid && Position == other.Position && Sequence == other.Sequence;

    public override int GetHashCode() => HashCode.Combine(Valid, Position, Sequence);

    bool PrintMembers(StringBuilder builder) {
        builder.Append($"Position: {Position}, Sequence: {Sequence}, Timestamp: {Timestamp:O}");

        return true;
    }
}

public delegate ValueTask<Checkpoint> CommitCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken);
