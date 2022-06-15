// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Checkpoints;
using static Eventuous.Subscriptions.Checkpoints.CheckpointCommitHandler;

namespace Eventuous.Subscriptions.Diagnostics;

sealed class CheckpointCommitMetrics : IDisposable {
    readonly IDisposable                               _commitHandlerSub;
    readonly ConcurrentDictionary<string, CommitEvent> _commitEvents = new();

    public CheckpointCommitMetrics()
        => _commitHandlerSub = DiagnosticListener.AllListeners.Subscribe(
            listener => {
                if (listener.Name != CheckpointCommitHandler.DiagnosticName) return;

                listener.Subscribe(RecordCheckpointCommit);
            }
        );

    void RecordCheckpointCommit(KeyValuePair<string, object?> evt) {
        var (key, value) = evt;

        if (key != CommitOperation || value is not CommitEvent commitEvent) return;

        _commitEvents[commitEvent.Id] = commitEvent;
    }

    public void Dispose() => _commitHandlerSub.Dispose();

    public IEnumerable<Measurement<long>> Record()
        => _commitEvents
            .Where(x => x.Value.FirstPending.HasValue)
            .Select(
                x => new Measurement<long>(
                    (long)(x.Value.CommitPosition.Sequence - x.Value.FirstPending!.Value.Sequence),
                    EventuousDiagnostics.CombineWithDefaultTags(
                        new KeyValuePair<string, object?>(SubscriptionMetrics.SubscriptionIdTag, x.Value.Id)
                    )
                )
            );
}
