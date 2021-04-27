using System.Threading;
using System.Threading.Tasks;

namespace Eventuous.Subscriptions {
    public class NoOpCheckpointStore : ICheckpointStore {
        public ValueTask<Checkpoint> GetLastCheckpoint(
            string            checkpointId,
            CancellationToken cancellationToken = default
        )
            => new(new Checkpoint(checkpointId, 0));

        public ValueTask<Checkpoint> StoreCheckpoint(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken = default
        )
            => new(checkpoint);
    }
}