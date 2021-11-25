using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using static Eventuous.Subscriptions.Checkpoints.CheckpointCommitHandler;

namespace Eventuous.Subscriptions.Diagnostics;

sealed class CheckpointCommitMetrics : IDisposable {
    readonly IDisposable                               _commitHandlerSub;
    readonly ConcurrentDictionary<string, CommitEvent> _commitEvents = new();

    public CheckpointCommitMetrics() {
        _commitHandlerSub = DiagnosticListener.AllListeners.Subscribe(
            listener => {
                if (listener.Name != DiagnosticName) return;

                listener.Subscribe(RecordCheckpointCommit);
            }
        );
    }

    void RecordCheckpointCommit(KeyValuePair<string, object?> evt) {
        var (key, value) = evt;

        if (key != CommitOperation || value is not CommitEvent commitEvent) return;

        _commitEvents[commitEvent.Id] = commitEvent;
    }

    public void Dispose() => _commitHandlerSub.Dispose();

    public IEnumerable<Measurement<long>> Record()
        => _commitEvents
            .Where(x => x.Value.FirstPending != null)
            .Select(
                x => new Measurement<long>(
                    (long)(x.Value.CommitPosition.Sequence - x.Value.FirstPending!.Sequence),
                    new KeyValuePair<string, object?>(SubscriptionMetrics.SubscriptionIdTag, x.Value.Id)
                )
            );
}