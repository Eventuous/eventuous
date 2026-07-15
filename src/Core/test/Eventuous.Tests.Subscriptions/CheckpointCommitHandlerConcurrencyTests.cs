using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Checkpoints;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Guards against the production wedge (AI-1329) where <see cref="CheckpointCommitHandler.Commit"/>
/// read the non-thread-safe <c>_positions</c> <see cref="SortedSet{T}"/> ON THE ACK CALLER'S THREAD
/// (to build the commit diagnostic's <c>FirstPending</c>) while the commit worker thread mutated it —
/// a data race that threw <see cref="NullReferenceException"/> out of the ack path and dropped the
/// subscription. The commit diagnostic must be emitted from the worker thread, where <c>_positions</c>
/// is owned, so it can never race the caller.
/// </summary>
public class CheckpointCommitHandlerConcurrencyTests {
    /// <summary>
    /// Captures the managed thread id on which the commit diagnostic is emitted. Attaching any
    /// listener with the commit-handler diagnostic name also flips <c>Diagnostic.IsEnabled("Commit")</c>
    /// to true — exactly what <c>AddEventuousSubscriptions()</c> does in a real app, and why production
    /// hit this while the test suite (no listener) never had.
    /// </summary>
    sealed class ThreadCapturingListener(string subscriptionId, TaskCompletionSource<int> emittedOn)
        : GenericListener(CheckpointCommitHandler.DiagnosticName), IDisposable {
        // The commit DiagnosticListener is process-global, so filter to OUR handler's subscription id
        // (CommitEvent.Id) — otherwise a concurrently-running test's handler could be captured. The
        // event type is internal, so read Id reflectively. Capture the thread DiagnosticSource.Write
        // ran on; it is synchronous, so this is the emitting thread.
        protected override void OnEvent(KeyValuePair<string, object?> evt) {
            var id = evt.Value?.GetType().GetProperty("Id")?.GetValue(evt.Value) as string;

            if (id == subscriptionId) emittedOn.TrySetResult(Environment.CurrentManagedThreadId);
        }
    }

    [Test]
    public async Task Commit_diagnostic_is_not_emitted_on_the_caller_thread(CancellationToken ct) {
        var subscriptionId = $"test-commit-thread-{Guid.NewGuid():N}";
        var emittedOn      = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var listener = new ThreadCapturingListener(subscriptionId, emittedOn);

        var store = new NoOpCheckpointStore();

        await using var handler = new CheckpointCommitHandler(subscriptionId, store, TimeSpan.FromMilliseconds(1), batchSize: 1);

        // Call Commit from a DEDICATED thread. Its ManagedThreadId is never a thread-pool id, and the
        // worker runs on the pool — so "emitted != caller" is deterministic (comparing two pool threads
        // could collide under load). Before the fix Commit emits synchronously on this dedicated thread;
        // after it, emission happens on the worker.
        var callerThreadId = 0;
        var caller = new Thread(() => {
            callerThreadId = Environment.CurrentManagedThreadId;
            handler.Commit(new CommitPosition(0, 0, DateTime.UtcNow), ct).AsTask().GetAwaiter().GetResult();
        }) { IsBackground = true };
        caller.Start();
        caller.Join();

        var emittedThreadId = await emittedOn.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);

        emittedThreadId.ShouldNotBe(
            callerThreadId,
            "the commit diagnostic reads the worker-owned _positions, so it must be emitted on the worker thread, never the ack caller thread"
        );
    }
}
