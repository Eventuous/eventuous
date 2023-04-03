using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Eventuous.Tools;
using Microsoft.Extensions.Logging;

namespace Eventuous.Redis.Subscriptions;

public class RedisCheckpointStore : ICheckpointStore {

    readonly GetRedisDatabase       _getDatabase;
    readonly ILoggerFactory?        _loggerFactory;

    public RedisCheckpointStore(GetRedisDatabase getDatabase, ILoggerFactory? loggerFactory) {
        _getDatabase = getDatabase;
        _loggerFactory = loggerFactory;
    }

    public async ValueTask<Checkpoint> GetLastCheckpoint(
        string checkpointId, 
        CancellationToken cancellationToken
    ) { 
        Checkpoint checkpoint; 
        Logger.ConfigureIfNull(checkpointId, _loggerFactory);
        var position = await _getDatabase().StringGetAsync(checkpointId);
        checkpoint = position.IsNull ? Checkpoint.Empty(checkpointId) : new Checkpoint(checkpointId, Convert.ToUInt64(position));
        Logger.Current.CheckpointLoaded(this, checkpoint);
        return checkpoint;
    }

    public async ValueTask<Checkpoint> StoreCheckpoint(
        Checkpoint        checkpoint,
        bool              force,
        CancellationToken cancellationToken
    ) {
        if (checkpoint.Position == null) return checkpoint;
        await _getDatabase().StringSetAsync(checkpoint.Id, checkpoint.Position);
        return checkpoint;
    }
}

