namespace Eventuous.Tests.EventStore.Fixtures; 

public class TestCheckpointStore : ICheckpointStore {
    readonly Dictionary<string, Checkpoint> _checkpoints = new();
    
    public ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        var checkpoint = _checkpoints.TryGetValue(checkpointId, out var cp) ? cp : new(checkpointId, null);
        Logger.Current.CheckpointLoaded(this, checkpoint);
        return new(checkpoint);
    }

    public ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
        Logger.Current.CheckpointStored(this, checkpoint, force);
        _checkpoints[checkpoint.Id] = checkpoint;
        return new(checkpoint);
    }
    
    public ulong? GetCheckpoint(string checkpointId) => _checkpoints.TryGetValue(checkpointId, out var cp) ? cp.Position : null;
}