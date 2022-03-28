using Eventuous.Subscriptions.Diagnostics;

namespace Eventuous.Subscriptions.Checkpoints;

public class NoOpCheckpointStore : ICheckpointStore {
    readonly Checkpoint _start;

    public NoOpCheckpointStore(ulong? start = null) => _start = new Checkpoint("", start);

    public ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        SubscriptionsEventSource.Log.CheckpointLoaded(this, _start);
        return new ValueTask<Checkpoint>(_start);
    }

    public ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
        SubscriptionsEventSource.Log.CheckpointStored(this, checkpoint);
        return new ValueTask<Checkpoint>(checkpoint);
    }
}