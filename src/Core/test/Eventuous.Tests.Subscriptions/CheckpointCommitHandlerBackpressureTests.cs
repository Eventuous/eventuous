using Eventuous.Subscriptions.Checkpoints;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Pins the channel-backpressure behaviour <see cref="CheckpointCommitHandler"/> switched to
/// (instead of throwing) when the commit queue fills up while the checkpoint store is slow: a
/// dropped <see cref="CommitPosition"/> is poison — a gap in the sequence stalls checkpoint
/// progression permanently — so overflow must throttle the caller, not fail the commit.
/// </summary>
public class CheckpointCommitHandlerBackpressureTests {
    static CommitPosition Pos(ulong sequence) => new(sequence, sequence, DateTime.UtcNow);

    [Test]
    [Timeout(20_000)]
    public async Task Commit_awaits_capacity_instead_of_throwing_when_the_channel_is_full(CancellationToken cancellationToken) {
        List<ulong>          committed    = [];
        TaskCompletionSource storeGate    = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource storeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var handler = new CheckpointCommitHandler("backpressure-sub", CommitFn, TimeSpan.FromMilliseconds(10), batchSize: 1);

        // The gate must open no matter how the test body ends: a failed assertion with the gate
        // still closed would leave the worker parked in CommitFn and the `await using` disposal
        // hanging until the test timeout — masking the clean assertion failure.
        try {
            // Sequence 0 is picked up by the worker and blocks inside CommitFn on the stalled gate —
            // this is the "current commit in flight" the rest of the batch queues behind. The entered
            // signal proves the worker actually dequeued it before we start filling the channel.
            await handler.Commit(Pos(0), cancellationToken);
            await storeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            // Fill the bounded channel: capacity is batchSize * 1000 = 1000.
            for (ulong sequence = 1; sequence <= 1000; sequence++) {
                await handler.Commit(Pos(sequence), cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }

            // The channel is now full and the worker is still blocked on sequence 0: the next Commit
            // call has nowhere to enqueue to. It must not throw — it should await channel capacity.
            var overflow = handler.Commit(Pos(1001), cancellationToken);
            await Task.Delay(200, cancellationToken);
            overflow.IsCompleted.ShouldBeFalse("Commit should be backpressured (awaiting channel capacity), not completed or throwing");

            // Let the store recover: the worker drains the backlog, one sequence at a time.
            storeGate.TrySetResult();

            await overflow.AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            // Poll until the store has recorded sequence 1001, then assert nothing was skipped along
            // the way: because the commit batch size is 1 and the run is fully contiguous, every single
            // sequence from 0 to 1001 must have gone through CommitFn, in order.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            List<ulong> snapshot;

            while (true) {
                lock (committed) snapshot = [..committed];

                if (snapshot.Count > 0 && snapshot[^1] == 1001) break;

                if (DateTime.UtcNow > deadline) break;

                await Task.Delay(50, cancellationToken);
            }

            snapshot.ShouldBe([.. Enumerable.Range(0, 1002).Select(i => (ulong)i)]);
        } finally {
            storeGate.TrySetResult();
        }

        return;

        async ValueTask<Checkpoint> CommitFn(Checkpoint checkpoint, bool force, CancellationToken ct) {
            storeEntered.TrySetResult();
            await storeGate.Task.WaitAsync(ct);
            lock (committed) committed.Add(checkpoint.Position!.Value);

            return checkpoint;
        }
    }

    [Test]
    [Timeout(20_000)]
    public async Task Dispose_releases_a_backpressured_commit_and_drains_without_hanging(CancellationToken cancellationToken) {
        List<(ulong Position, bool Force)> committed    = [];
        TaskCompletionSource               storeGate    = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource               storeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var handler = new CheckpointCommitHandler("backpressure-dispose-sub", CommitFn, TimeSpan.FromMilliseconds(10), batchSize: 1);

        Task? disposeTask = null;

        // Two guarantees on every exit path: the gate opens (a failed assertion with the gate still
        // closed would otherwise leave the worker parked in CommitFn and disposal hanging until the
        // test timeout), and the handler's disposal is awaited (bounded) so a failure doesn't leak
        // a live worker into the rest of the run.
        try {
            // Sequence 0 is dequeued by the worker and blocks in CommitFn on the still-stalled gate.
            await handler.Commit(Pos(0), cancellationToken);
            await storeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            // Fill the bounded channel (capacity batchSize * 1000 = 1000) and park one more Commit in
            // backpressure — a writer genuinely awaiting channel capacity when Dispose begins.
            for (ulong sequence = 1; sequence <= 1000; sequence++) {
                await handler.Commit(Pos(sequence), cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }

            var overflow = handler.Commit(Pos(1001), cancellationToken);
            await Task.Delay(200, cancellationToken);
            overflow.IsCompleted.ShouldBeFalse("The overflow Commit should be backpressured before Dispose begins");

            // The parked caller must come back rather than hang, and be told its position never made it —
            // silence here would let the acknowledgement path treat a dropped position as committed.
            disposeTask = handler.DisposeAsync().AsTask();

            // Generous rather than tight: the release is immediate, but happens on disposal's thread while the store is stalled.
            var accepted = await overflow.AsTask().WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);

            accepted.ShouldBeFalse("a Commit released by disposal never queued its position, and has to say so");

            // Let the store recover so dispose can drain the queued positions and run its final
            // force-commit within its own internal bounds.
            storeGate.TrySetResult();

            var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
            completed.ShouldBe(disposeTask, "DisposeAsync should complete once the store recovers, not hang");
            await disposeTask;

            List<(ulong Position, bool Force)> snapshot;
            lock (committed) snapshot = [..committed];

            snapshot.ShouldNotBeEmpty();

            // The queued positions (0..1000) drained normally, in order, during dispose; the parked
            // overflow write (1001) never entered the channel, so it must not appear.
            var normal = snapshot.Where(x => !x.Force).Select(x => x.Position).ToList();
            normal.ShouldBe([.. Enumerable.Range(0, 1001).Select(i => (ulong)i)]);

            // CheckpointCommitHandler's OnDispose force-recommits whatever it last successfully stored —
            // it should match the highest position that drained normally, not something stale or ahead
            // of it.
            var forced = snapshot.Where(x => x.Force).ToList();
            forced.ShouldNotBeEmpty();
            forced[^1].Position.ShouldBe(normal[^1]);
        } finally {
            storeGate.TrySetResult();

            // Bounded so a genuinely hung disposal surfaces the original assertion failure instead
            // of stalling the finally block until the test timeout.
            await Task.WhenAny(disposeTask ?? handler.DisposeAsync().AsTask(), Task.Delay(TimeSpan.FromSeconds(10)));
        }

        return;

        async ValueTask<Checkpoint> CommitFn(Checkpoint checkpoint, bool force, CancellationToken ct) {
            storeEntered.TrySetResult();
            await storeGate.Task.WaitAsync(ct);
            lock (committed) committed.Add((checkpoint.Position!.Value, force));

            return checkpoint;
        }
    }
}
