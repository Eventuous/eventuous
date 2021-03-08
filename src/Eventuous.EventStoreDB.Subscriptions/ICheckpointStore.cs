using System.Threading;
using System.Threading.Tasks;

namespace Eventuous.EventStoreDB.Subscriptions {
    public record Checkpoint(string Id, ulong? Position);

    public interface ICheckpointStore {
        ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken = default);

        ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, CancellationToken cancellationToken = default);
    }
}
