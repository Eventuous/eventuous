using Eventuous.Subscriptions.Diagnostics;

namespace Eventuous.Tests.EventStore.Fixtures; 

public class TestCheckpointStore : ICheckpointStore {
    readonly Checkpoint _start;
    
    public Checkpoint Last { get; private set; }

    public TestCheckpointStore(ulong? start = null) {
        _start = new Checkpoint("", start);
        Last   = _start;
    }

    public ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        SubscriptionsEventSource.Log.CheckpointLoaded(this, _start);
        return new ValueTask<Checkpoint>(_start);
    }

    public ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
        Last = checkpoint;
        SubscriptionsEventSource.Log.CheckpointStored(this, checkpoint);
        return new ValueTask<Checkpoint>(checkpoint);
    }
}