// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;
using Eventuous.Diagnostics;

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous.Subscriptions.Checkpoints;

public class CheckpointLog {
    static readonly CheckpointEventSource EventSource = new();

    readonly string _id;

    public CheckpointLog(string id) => _id = id;

    [NonEvent]
    public void PositionReceived(CommitPosition position) {
        if (EventSource.IsEnabled(EventLevel.Verbose, EventKeywords.All))
            EventSource.PositionReceived(_id, position.Position);
    }

    [NonEvent]
    public void UnableToCommit(Exception exception) {
        if (EventSource.IsEnabled(EventLevel.Warning, EventKeywords.All))
            EventSource.UnableToCommit(_id, exception.Message);
    }

    [NonEvent]
    public void Stopping() {
        if (EventSource.IsEnabled(EventLevel.Informational, EventKeywords.All)) 
            EventSource.Stopping(_id);
    }

    [NonEvent]
    public void Committing(CommitPosition position) {
        if (EventSource.IsEnabled(EventLevel.Verbose, EventKeywords.All))
            EventSource.Committing(_id, (long)position.Position);
    }
}

[EventSource(Name = $"{DiagnosticName.BaseName}-checkpoint")]
public class CheckpointEventSource : EventSource {
    const int CheckpointReceivedId = 1;
    const int UnableToCommitId     = 2;
    const int StoppingId           = 3;
    const int CommittingId         = 4;

    [Event(CheckpointReceivedId, Message = "[{0}] Checkpoint received: '{1}'", Level = EventLevel.Verbose)]
    public void PositionReceived(string id, ulong position) => WriteEvent(CheckpointReceivedId, id, position);

    [Event(UnableToCommitId, Message = "[{0}] Unable to commit checkpoint: {1}")]
    public void UnableToCommit(string id, string message) => WriteEvent(UnableToCommitId, id, message);

    [Event(StoppingId, Message = "[{0}] Stopping commit handler worker")]
    public void Stopping(string id) => WriteEvent(StoppingId, id);

    [Event(CommittingId, Message = "[{0}] Committing to store: {1}")]
    public void Committing(string id, long checkpoint) => WriteEvent(CommittingId, id, checkpoint);
}
