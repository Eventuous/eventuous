namespace Eventuous.Subscriptions; 

public class NoOpCheckpointStore : ICheckpointStore {
    readonly Checkpoint _start;

    public NoOpCheckpointStore(ulong? start = null)
        => _start = new Checkpoint("", start);

    public ValueTask<Checkpoint> GetLastCheckpoint(
        string            checkpointId,
        CancellationToken cancellationToken = default
    )
        => new(_start);

    public ValueTask<Checkpoint> StoreCheckpoint(
        Checkpoint        checkpoint,
        CancellationToken cancellationToken = default
    )
        => new(checkpoint);
}