// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

using System.Runtime.CompilerServices;

namespace Eventuous.Subscriptions.Logging;

using Checkpoints;

public static class CheckpointLogging {
    extension(LogContext log) {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PositionReceived(CommitPosition checkpoint)
            => log.TraceLog?.Log("Received checkpoint: {Position}", checkpoint);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CommittingPosition(CommitPosition position)
            => log.DebugLog?.Log("Committing position {Position}", position);

        public void UnableToCommitPosition(CommitPosition position, Exception exception)
            => log.ErrorLog?.Log(exception, "Unable to commit position {Position}", position);
    }

    extension(LogContext? log) {
        public void CheckpointLoaded(ICheckpointStore store, Checkpoint checkpoint)
            => log?.InfoLog?.Log("Loaded checkpoint {CheckpointId} from {Store}: {Position}", checkpoint.Id, store.GetType().Name, checkpoint);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CheckpointStored(ICheckpointStore store, Checkpoint checkpoint, bool force) {
            if (log == null) return;

            const string message = "Stored checkpoint {CheckpointId} in {Store}: {Position}";

            if (force) log.InfoLog?.Log(message, checkpoint.Id, store.GetType().Name, checkpoint);
            else log.TraceLog?.Log(message, checkpoint.Id, store.GetType().Name, checkpoint);
        }
    }
}
