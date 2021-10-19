using System.Threading.Channels;

namespace Eventuous.Subscriptions.Checkpoints;

public class CheckpointCommitHandler : IAsyncDisposable {
    readonly string                  _subscriptionId;
    readonly CommitCheckpoint        _commitCheckpoint;
    readonly int                     _batchSize;
    readonly CommitPositionSequence      _positions = new();
    readonly Channel<CommitPosition> _channel;
    readonly CancellationTokenSource _cts;

    public CheckpointCommitHandler(string subscriptionId, CommitCheckpoint commitCheckpoint, int batchSize = 1) {
        _subscriptionId   = subscriptionId;
        _commitCheckpoint = commitCheckpoint;
        _batchSize        = batchSize;
        _channel          = Channel.CreateBounded<CommitPosition>(batchSize * 10);

        _cts = new CancellationTokenSource();
        Task.Run(() => Reader(_cts.Token));
    }

    async Task Reader(CancellationToken cancellationToken) {
        try {
            while (!cancellationToken.IsCancellationRequested && !_channel.Reader.Completion.IsCompleted) {
                var position = await _channel.Reader.ReadAsync(cancellationToken);
                _positions.Add(position);
                if (_positions.Count < _batchSize) return;

                await CommitInternal(cancellationToken);
            }
        }
        catch (OperationCanceledException) {
            // it's ok
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
        => _channel.Writer.WriteAsync(position, cancellationToken);

    async ValueTask CommitInternal(CancellationToken cancellationToken) {
        var commitPosition = _positions.FirstBeforeGap();
        if (!commitPosition.Valid) return;

        await _commitCheckpoint(new Checkpoint(_subscriptionId, commitPosition.Position), cancellationToken);
        _positions.Clear();
    }

    public async ValueTask DisposeAsync() {
        _channel.Writer.Complete();
        _cts.CancelAfter(TimeSpan.FromSeconds(1));

        while (_channel.Reader.Completion.IsCompleted && !_cts.IsCancellationRequested) {
            await Task.Delay(10);
        }

        if (!_cts.IsCancellationRequested) {
            await CommitInternal(_cts.Token);
        }

        _positions.Clear();
        GC.SuppressFinalize(this);
    }
}

public record CommitPosition(ulong Position, ulong Sequence) {
    public bool Valid { get; init; } = true;

    public static CommitPosition None = new(0, 0) { Valid = false };
}

public delegate ValueTask<Checkpoint> CommitCheckpoint(Checkpoint checkpoint, CancellationToken cancellationToken);