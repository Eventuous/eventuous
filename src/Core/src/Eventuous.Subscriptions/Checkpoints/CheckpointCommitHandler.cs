using System.Threading.Channels;
using Eventuous.Subscriptions.Channels;

namespace Eventuous.Subscriptions.Checkpoints;

public class CheckpointCommitHandler : IAsyncDisposable {
    readonly string                        _subscriptionId;
    readonly CommitCheckpoint              _commitCheckpoint;
    readonly CommitPositionSequence        _positions = new();
    readonly ChannelWorker<CommitPosition> _worker;

    public CheckpointCommitHandler(string subscriptionId, CommitCheckpoint commitCheckpoint, int batchSize = 1) {
        _subscriptionId   = subscriptionId;
        _commitCheckpoint = commitCheckpoint;
        var channel = Channel.CreateBounded<CommitPosition>(batchSize * 10);
        _worker = new ChannelWorker<CommitPosition>(channel, Process, true);

        async ValueTask Process(CommitPosition position, CancellationToken cancellationToken) {
            _positions.Add(position);
            if (_positions.Count < batchSize) return;

            await CommitInternal(cancellationToken);
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
        
        return _worker.Write(position, cancellationToken);
    }

    async ValueTask CommitInternal(CancellationToken cancellationToken) {
        var commitPosition = _positions.FirstBeforeGap();
        if (!commitPosition.Valid) return;

        await _commitCheckpoint(new Checkpoint(_subscriptionId, commitPosition.Position), cancellationToken);
        _positions.Clear();
    }

    public async ValueTask DisposeAsync() {
        await _worker.Stop(CommitInternal);
        _positions.Clear();
        GC.SuppressFinalize(this);
    }
}

public record CommitPosition(ulong Position, ulong Sequence) {
    public bool Valid { get; private init; } = true;

    public static readonly CommitPosition None = new(0, 0) { Valid = false };
}

public delegate ValueTask<Checkpoint> CommitCheckpoint(Checkpoint checkpoint, CancellationToken cancellationToken);