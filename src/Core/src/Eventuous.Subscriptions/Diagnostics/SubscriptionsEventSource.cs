// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;
using Eventuous.Diagnostics;

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous.Subscriptions.Diagnostics;

using Checkpoints;

[EventSource(Name = $"{DiagnosticName.BaseName}-subscription")]
public class SubscriptionsEventSource : EventSource {
    public static readonly SubscriptionsEventSource Log = new();

    const int MetricCollectionFailedId        = 1;
    const int MessageTypeNotRegisteredId      = 2;
    const int CheckpointLastCommitGapId       = 101;
    const int CheckpointSequenceInvalidHeadId = 102;
    const int CheckpointAlreadyCommittedId    = 103;
    const int CheckpointGapDetectedId         = 104;
    const int CheckpointLastCommitDuplicateId = 105;

    [NonEvent]
    public void MetricCollectionFailed(string metric, Exception exception) => MetricCollectionFailed(metric, exception.ToString());

    [NonEvent]
    public void MessageTypeNotRegistered<T>() => MessageTypeNotRegistered(typeof(T).Name);

    [NonEvent]
    public void CheckpointAlreadyCommitted(string id, CommitPosition checkpoint) {
        if (IsEnabled(EventLevel.Verbose, Keywords.Checkpoints)) CheckpointAlreadyCommitted(id, checkpoint.Position);
    }

    [NonEvent]
    public void CheckpointLastCommitGap(CommitPosition lastCommitPosition, CommitPosition latestPosition) {
        if (IsEnabled(EventLevel.Verbose, Keywords.Checkpoints))
            CheckpointLastCommitGap(lastCommitPosition.Sequence, lastCommitPosition.Position, latestPosition.Sequence, latestPosition.Position);
    }

    [NonEvent]
    public void CheckpointLastCommitDuplicate(CommitPosition lastCommitPosition) {
        if (IsEnabled(EventLevel.Verbose, Keywords.Checkpoints)) CheckpointLastCommitDuplicate(lastCommitPosition.Sequence, lastCommitPosition.Position);
    }

    [NonEvent]
    public void CheckpointSequenceInvalidHead(CommitPosition latestPosition) {
        if (IsEnabled(EventLevel.Verbose, Keywords.Checkpoints)) CheckpointSequenceInvalidHead(latestPosition.Sequence, latestPosition.Position);
    }

    [NonEvent]
    public void CheckpointGapDetected(CommitPosition before, CommitPosition after) {
        if (IsEnabled(EventLevel.Verbose, Keywords.Checkpoints)) CheckpointGapDetected(before.Sequence, before.Position, after.Sequence, after.Position);
    }

    [Event(MetricCollectionFailedId, Message = "Failed to collect metric {0}: {1}", Level = EventLevel.Warning)]
    void MetricCollectionFailed(string metric, string exception) => WriteEvent(MetricCollectionFailedId, metric, exception);

    [Event(MessageTypeNotRegisteredId, Message = "Message type {0} is not registered", Level = EventLevel.Warning)]
    void MessageTypeNotRegistered(string messageType) => WriteEvent(MessageTypeNotRegisteredId, messageType);

    [Event(CheckpointAlreadyCommittedId, Message = "Checkpoint already committed {0}:{1}", Level = EventLevel.Verbose, Keywords = Keywords.Checkpoints)]
    void CheckpointAlreadyCommitted(string id, ulong position) => WriteEvent(CheckpointAlreadyCommittedId, id, position);

    [Event(
        CheckpointLastCommitGapId,
        Message = "Last commit position {0}:{1} is behind latest {2}:{3}",
        Level = EventLevel.Verbose,
        Keywords = Keywords.Checkpoints
    )]
    void CheckpointLastCommitGap(ulong lastCommitSequence, ulong lastCommitPosition, ulong latestSequence, ulong latestPosition)
        => WriteEvent(CheckpointLastCommitGapId, lastCommitSequence, lastCommitPosition, latestSequence, latestPosition);

    [Event(
        CheckpointLastCommitDuplicateId,
        Message = "Last commit position {0}:{1} equals the latest",
        Level = EventLevel.Warning,
        Keywords = Keywords.Checkpoints
    )]
    void CheckpointLastCommitDuplicate(ulong latestSequence, ulong latestPosition)
        => WriteEvent(CheckpointLastCommitDuplicateId, latestSequence, latestPosition);

    [Event(
        CheckpointSequenceInvalidHeadId,
        Message = "Last commit position {0}:{1} sequence is not zero",
        Level = EventLevel.Verbose,
        Keywords = Keywords.Checkpoints
    )]
    void CheckpointSequenceInvalidHead(ulong sequence, ulong position) => WriteEvent(CheckpointSequenceInvalidHeadId, sequence, position);

    [Event(
        CheckpointGapDetectedId,
        Message = "Gap detected in checkpoint between {0}:{1} and {2}:{3}",
        Level = EventLevel.Verbose,
        Keywords = Keywords.Checkpoints
    )]
    void CheckpointGapDetected(ulong beforeSequence, ulong beforePosition, ulong afterSequence, ulong afterPosition)
        => WriteEvent(CheckpointGapDetectedId, beforeSequence, beforePosition, afterSequence, afterPosition);

    public static class Keywords {
        public const EventKeywords Checkpoints = (EventKeywords)1024;
    }
}
