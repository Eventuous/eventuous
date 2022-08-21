// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Subscriptions.Checkpoints;

public class NoOpCheckpointStore : ICheckpointStore {
    Checkpoint _start;

    public NoOpCheckpointStore(ulong? start = null) => _start = new Checkpoint("", start);

    public ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        Log.CheckpointLoaded(this, _start);
        return new ValueTask<Checkpoint>(_start);
    }

    public ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
        _start = checkpoint;
        CheckpointStored(this, checkpoint);
        Log.CheckpointStored(this, checkpoint);
        return new ValueTask<Checkpoint>(checkpoint);
    }
     public event EventHandler<Checkpoint> CheckpointStored;
}