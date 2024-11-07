// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Checkpoints;

using Logging;

/// <summary>
/// Fake checkpoint store; can be useful for testing. It always starts at the beginning.
/// </summary>
/// <param name="start"></param>
public class NoOpCheckpointStore(ulong? start = null) : ICheckpointStore {
    Checkpoint _start = new("", start);

    public ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        var checkpoint = _start with { Id = checkpointId };
        Logger.Current.CheckpointLoaded(this, checkpoint);

        return new(checkpoint);
    }

    public ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
        _start = checkpoint;
        CheckpointStored?.Invoke(this, checkpoint);
        Logger.Current.CheckpointStored(this, checkpoint, force);

        return new(checkpoint);
    }

    public event EventHandler<Checkpoint>? CheckpointStored;
}
