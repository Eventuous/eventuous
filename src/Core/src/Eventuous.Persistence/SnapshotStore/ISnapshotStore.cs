// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Snapshot store for storing and retrieving snapshot events separately from the main event stream.
/// Used when <see cref="SnapshotStorageStrategy.SeparateStore"/> is configured.
/// </summary>
[PublicAPI]
public interface ISnapshotStore {
    /// <summary>
    /// Reads the latest snapshot for the specified stream
    /// </summary>
    /// <param name="streamName">Stream name to read snapshot for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Snapshot data if found, null otherwise</returns>
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    Task<Snapshot?> Read(StreamName streamName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a snapshot to the store
    /// </summary>
    /// <param name="streamName">Stream name to write snapshot for</param>
    /// <param name="snapshot">Snapshot data to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the write operation</returns>
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    Task Write(StreamName streamName, Snapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a snapshot for the specified stream
    /// </summary>
    /// <param name="streamName">Stream name to delete snapshot for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the delete operation</returns>
    Task Delete(StreamName streamName, CancellationToken cancellationToken = default);
}

