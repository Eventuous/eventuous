namespace Eventuous.Tests.EventStore.Fixtures; 

public class TestCheckpointStore : ICheckpointStore {
    readonly Checkpoint _start;
    
    public Checkpoint Last { get; private set; }

    public TestCheckpointStore(ulong? start = null) {
        _start = new Checkpoint("", start);
        Last   = _start;
    }

    public ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        return new ValueTask<Checkpoint>(_start);
    }

    public ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, CancellationToken cancellationToken) {
        Last = checkpoint;
        return new ValueTask<Checkpoint>(checkpoint);
    }
}