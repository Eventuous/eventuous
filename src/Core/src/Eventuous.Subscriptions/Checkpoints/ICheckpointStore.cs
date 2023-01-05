// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Checkpoints;

[PublicAPI]
public record struct Checkpoint(string Id, ulong? Position) {
    public static Checkpoint Empty(string id) => new(id, null);
}

[PublicAPI]
public interface ICheckpointStore {
    ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken);

    ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken);
}