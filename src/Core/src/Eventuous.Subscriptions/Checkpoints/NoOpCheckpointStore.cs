// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Checkpoints;

using Logging;

public class NoOpCheckpointStore(ulong? start = null) : ICheckpointStore {
    Checkpoint _start = new("", start);

    public ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        Logger.Current.CheckpointLoaded(this, _start);

        return new ValueTask<Checkpoint>(_start);
    }

    public ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
        _start = checkpoint;
        CheckpointStored?.Invoke(this, checkpoint);
        Logger.Current.CheckpointStored(this, checkpoint, force);

        return new ValueTask<Checkpoint>(checkpoint);
    }

    public event EventHandler<Checkpoint>? CheckpointStored;
}
