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

    public CheckpointCommitHandler(string subscriptionId, CommitCheckpoint commitCheckpoint, int batchSize = 1) {
        _subscriptionId   = subscriptionId;
        _commitCheckpoint = commitCheckpoint;
        var channel = Channel.CreateBounded<CommitPosition>(batchSize * 10);
        _worker = new ChannelWorker<CommitPosition>(channel, Process, true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        async ValueTask Process(CommitPosition position, CancellationToken cancellationToken) {
            _positions.Add(position);
            if (_positions.Count < batchSize) return;

            await CommitInternal(cancellationToken).NoContext();
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
    public ValueTask Commit(CommitPosition position, CancellationToken cancellationToken)
        => _worker.Write(position, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    async ValueTask CommitInternal(CancellationToken cancellationToken) {
        try {
            switch (_lastCommit.Valid) {
                // There's a gap between the last committed position and the list head
                case true when _lastCommit.Sequence + 1 != _positions.Min?.Sequence:
                // The list head is not at the very beginning
                case false when _positions.Min?.Sequence != 0:
                    return;
            }

            var commitPosition = _positions.FirstBeforeGap();
            if (!commitPosition.Valid) return;

            await _commitCheckpoint(
                new Checkpoint(_subscriptionId, commitPosition.Position),
                cancellationToken
            ).NoContext();

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
        await _worker.Stop(CommitInternal);
        _positions.Clear();
    }
}

public record CommitPosition(ulong Position, ulong Sequence) {
    public bool Valid { get; private init; } = true;

    public static readonly CommitPosition None = new(0, 0) { Valid = false };
}

public delegate ValueTask<Checkpoint> CommitCheckpoint(
    Checkpoint        checkpoint,
    CancellationToken cancellationToken
);