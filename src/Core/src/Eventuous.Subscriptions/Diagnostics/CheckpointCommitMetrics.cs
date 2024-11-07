// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;

namespace Eventuous.Subscriptions.Diagnostics;

using static Checkpoints.CheckpointCommitHandler;

sealed class CheckpointCommitMetrics() : GenericListener(DiagnosticName), IDisposable {
    readonly ConcurrentDictionary<string, CommitEvent> _commitEvents = new();

    protected override void OnEvent(KeyValuePair<string, object?> evt) {
        var (_, value) = evt;

        if (value is not CommitEvent commitEvent) return;

        _commitEvents[commitEvent.Id] = commitEvent;
    }

    public DateTime GetLastTimestamp(string subscriptionId)
        => _commitEvents.TryGetValue(subscriptionId, out var commitEvent) ? commitEvent.CommitPosition.Timestamp : DateTime.MinValue;

    public ulong GetLastCommitPosition(string subscriptionId)
        => _commitEvents.TryGetValue(subscriptionId, out var commitEvent) ? commitEvent.CommitPosition.Position : 0;

    public IEnumerable<Measurement<long>> Record()
        => _commitEvents
            .Where(x => x.Value.FirstPending.HasValue)
            .Select(
                x => new Measurement<long>(
                    (long)(x.Value.CommitPosition.Sequence - x.Value.FirstPending!.Value.Sequence),
                    EventuousDiagnostics.CombineWithDefaultTags(new KeyValuePair<string, object?>(SubscriptionMetrics.SubscriptionIdTag, x.Value.Id))
                )
            );
}
