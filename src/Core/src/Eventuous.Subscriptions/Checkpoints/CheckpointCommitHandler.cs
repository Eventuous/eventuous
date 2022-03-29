using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Eventuous.Subscriptions.Channels;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Subscriptions.Checkpoints;

public sealed class CheckpointCommitHandler : IAsyncDisposable {
    readonly string                        _subscriptionId;
    readonly CommitCheckpoint              _commitCheckpoint;
    readonly CommitPositionSequence        _positions = new();
    readonly ChannelWorker<CommitPosition> _worker;

    CommitPosition _lastCommit = CommitPosition.None;

    public const string DiagnosticName  = "eventuous.checkpoint.commithandler";
    public const string CommitOperation = "Commit";

    static readonly DiagnosticSource Diagnostic = new DiagnosticListener(DiagnosticName);

    internal record CommitEvent(string Id, CommitPosition CommitPosition, CommitPosition? FirstPending);

    public CheckpointCommitHandler(string subscriptionId, CommitCheckpoint commitCheckpoint, int batchSize = 1) {
        _subscriptionId   = subscriptionId;
        _commitCheckpoint = commitCheckpoint;
        var channel = Channel.CreateBounded<CommitPosition>(batchSize * 1000);
        _worker = new ChannelWorker<CommitPosition>(channel, Process, true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        async ValueTask Process(CommitPosition position, CancellationToken cancellationToken) {
            _positions.Add(position);
            if (_positions.Count < batchSize) return;

            await CommitInternal(false, cancellationToken).NoContext();
        }
    }

    public CheckpointCommitHandler(string subscriptionId, ICheckpointStore checkpointStore, int batchSize = 1)
        : this(subscriptionId, checkpointStore.StoreCheckpoint, batchSize) { }

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
    async ValueTask CommitInternal(bool force, CancellationToken cancellationToken) {
        try {
            switch (_lastCommit.Valid) {
                // There's a gap between the last committed position and the list head
                case true when _lastCommit.Sequence + 1 != _positions.Min.Sequence && !force:
                // The list head is not at the very beginning
                case false when _positions.Min.Sequence != 0:
                    return;
            }

            var commitPosition = _positions.FirstBeforeGap();
            if (!commitPosition.Valid) return;

            await _commitCheckpoint(
                    new Checkpoint(_subscriptionId, commitPosition.Position),
                    force,
                    cancellationToken
                )
                .NoContext();

            _lastCommit = commitPosition;

            // Removing positions before and including the committed one
            _positions.RemoveWhere(x => x.Sequence <= commitPosition.Sequence);
        }
        catch (Exception e) {
            Log.Warn("Error committing", e.ToString());
        }
    }

    public async ValueTask DisposeAsync() {
        Log.Stopping(nameof(CheckpointCommitHandler), "worker", "");
        await _worker.Stop(ct => CommitInternal(true, ct)).NoContext();
        _positions.Clear();
    }
}

public record struct CommitPosition(ulong Position, ulong Sequence) {
    public bool Valid { get; private init; } = true;

    public static readonly CommitPosition None = new(0, 0) { Valid = false };
}

public delegate ValueTask<Checkpoint> CommitCheckpoint(
    Checkpoint        checkpoint,
    bool              force,
    CancellationToken cancellationToken
);
