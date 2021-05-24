using System.Threading;
using System.Threading.Tasks;
using Eventuous.Subscriptions;

namespace Eventuous.Tests.SqlStreamStore.PubSub {
    public class InMemoryCheckpointStore : ICheckpointStore {
        ulong? _position;
        public InMemoryCheckpointStore(ulong? position = null) => _position = position;
        public ValueTask<Checkpoint> GetLastCheckpoint(
            string            checkpointId,
            CancellationToken cancellationToken = default
        )
            => new(new Checkpoint(checkpointId, _position));

        public ValueTask<Checkpoint> StoreCheckpoint(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken = default
        )
        {
            _position = checkpoint.Position;
            return ValueTask.FromResult(new Checkpoint(checkpoint.Id, _position));
        }
    }
}