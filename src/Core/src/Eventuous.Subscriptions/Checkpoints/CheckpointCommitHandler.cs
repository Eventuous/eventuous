// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Eventuous.Subscriptions.Channels;
using Eventuous.Subscriptions.Logging;
using Eventuous.Tools;
using Microsoft.Extensions.Logging;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Subscriptions.Checkpoints;

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
        int              batchSize     = 1,
        ILoggerFactory?  loggerFactory = null
    )
        : this(subscriptionId, checkpointStore.StoreCheckpoint, batchSize, loggerFactory) { }

    public CheckpointCommitHandler(
        string           subscriptionId,
        CommitCheckpoint commitCheckpoint,
        int              batchSize     = 1,
        ILoggerFactory?  loggerFactory = null
    ) {
        _subscriptionId   = subscriptionId;
        _commitCheckpoint = commitCheckpoint;
        _loggerFactory    = loggerFactory;
        var channel = Channel.CreateBounded<CommitPosition>(batchSize * 1000);
        _subject = new Subject<CommitPosition>();

        _subject
            .Buffer(TimeSpan.FromSeconds(5), batchSize)
            .Where(x => x.Count > 0)
            .Select(AddBatchAndGetLast)
            .Where(x => x.Valid)
            .Select(x => Observable.FromAsync(ct => CommitInternal(x, ct)))
            .Concat()
            .Subscribe();

        _worker = new ChannelWorker<CommitPosition>(channel, Process, true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValueTask Process(CommitPosition position, CancellationToken cancellationToken) {
            position.LogContext.PositionReceived(position);
            _subject.OnNext(position);
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        CommitPosition AddBatchAndGetLast(IList<CommitPosition> list) {
            _positions.UnionWith(list);
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
        if (Diagnostic.IsEnabled(CommitOperation))
            Diagnostic.Write(CommitOperation, new CommitEvent(_subscriptionId, position, _positions.Min));

        return _worker.Write(position, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    CommitPosition GetCommitPosition(bool force) {
        switch (_lastCommit.Valid) {
            // There's a gap between the last committed position and the list head
            case true when _lastCommit.Sequence + 1 != _positions.Min.Sequence && !force:
                Log.CheckpointLastCommitGap(_lastCommit, _positions.Min);
                return CommitPosition.None;
            // The list head is not at the very beginning
            case false when _positions.Min.Sequence != 0:
                Log.CheckpointSequenceInvalidHead(_positions.Min);
                return CommitPosition.None;
            case true when _lastCommit.Sequence == _positions.Min.Sequence:
                Log.CheckpointLastCommitDuplicate(_positions.Min);
                break;
        }

        return _positions.FirstBeforeGap();
    }

    async Task CommitInternal(CommitPosition position, CancellationToken cancellationToken) {
        try {
            if (_lastCommit == position) {
                Log.CheckpointAlreadyCommitted(_subscriptionId, position);
                return;
            }

            position.LogContext.CommittingPosition(position);

            await _commitCheckpoint(
                    new Checkpoint(_subscriptionId, position.Position),
                    false,
                    cancellationToken
                )
                .NoContext();

            _lastCommit = position;

            // Removing positions before and including the committed one
            _positions.RemoveWhere(x => x.Sequence <= position.Sequence);
        }
        catch (Exception e) {
            position.LogContext.UnableToCommitPosition(position, e);
        }
    }

    public async ValueTask DisposeAsync() {
        Logger.ConfigureIfNull(_subscriptionId, _loggerFactory);
        Logger.Current.InfoLog?.Log("Stopping commit handler worker");

        await _worker.Stop(
                _ => {
                    _subject.OnCompleted();
                    _subject.Dispose();
                    return default;
                }
            )
            .NoContext();

        _positions.Clear();
    }
}

public record struct CommitPosition(ulong Position, ulong Sequence, DateTime Timestamp) {
    public bool Valid { get; private init; } = true;

    public LogContext LogContext { get; init; }

    public static readonly CommitPosition None = new(0, 0, DateTime.MinValue) { Valid = false };
}

public delegate ValueTask<Checkpoint> CommitCheckpoint(
    Checkpoint        checkpoint,
    bool              force,
    CancellationToken cancellationToken
);