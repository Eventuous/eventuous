// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;

namespace Eventuous.Redis.Subscriptions;

public class RedisCheckpointStore(GetRedisDatabase getDatabase, ILoggerFactory? loggerFactory) : ICheckpointStore {
    public async ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        Logger.ConfigureIfNull(checkpointId, loggerFactory);
        var position   = await getDatabase().StringGetAsync(checkpointId).NoContext();
        var checkpoint = position.IsNull ? Checkpoint.Empty(checkpointId) : new Checkpoint(checkpointId, Convert.ToUInt64(position));
        Logger.Current.CheckpointLoaded(this, checkpoint);
        return checkpoint;
    }

    public async ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
        if (checkpoint.Position == null) return checkpoint;

        await getDatabase().StringSetAsync(checkpoint.Id, checkpoint.Position).NoContext();
        return checkpoint;
    }
}
