// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Checkpoints;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// A handler belongs to one run and lasts exactly as long as it does — it owns whether it still accepts
/// positions, so replacing the run underneath a dispatched commit can't redirect it elsewhere.
/// </summary>
public class CheckpointCommitHandlerLifecycleTests {
    [Test]
    public async Task An_open_handler_commits() {
        var (handler, committed) = Build();

        var accepted = await handler.Commit(Position(7, sequence: 0), CancellationToken.None);

        accepted.ShouldBeTrue();

        // Commits are batched onto the handler's own worker, so the store sees them a beat later.
        var stored = await Wait.Until(() => committed().Contains(7UL), TimeSpan.FromSeconds(5));

        stored.ShouldBeTrue("an accepted commit never reached the store");

        await handler.DisposeAsync();
    }

    /// <summary>
    /// A commit that claimed success after the handler stopped would acknowledge a message nothing is
    /// going to store.
    /// </summary>
    [Test]
    public async Task A_stopped_handler_refuses() {
        var (handler, committed) = Build();

        await handler.Commit(Position(7, sequence: 0), CancellationToken.None);
        (await Wait.Until(() => committed().Contains(7UL), TimeSpan.FromSeconds(5))).ShouldBeTrue();

        await handler.DisposeAsync();

        var accepted = await handler.Commit(Position(8, sequence: 1), CancellationToken.None);

        accepted.ShouldBeFalse("a commit into a stopped handler was reported as accepted");

        committed().ShouldNotContain(8UL);
    }

    /// <summary>
    /// A commit parked on backpressure when disposal completes the channel must come back refused — its
    /// position was never queued, so reporting success would drop it from the checkpoint.
    /// </summary>
    /// <remarks>
    /// Disposal itself still blocks here, retrying its final checkpoint flush without a token by design, so
    /// this asserts on the parked commit's result, not on disposal finishing.
    /// </remarks>
    [Test]
    [Timeout(60_000)]
    public async Task A_commit_parked_at_disposal_is_refused(CancellationToken cancellationToken) {
        var inStore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Blocking the store blocks the worker, filling the channel and parking a commit inside the handler.
        var handler = new CheckpointCommitHandler(
            "dispose-refuses",
            async (checkpoint, _, ct) => {
                inStore.TrySetResult();
                await release.Task.WaitAsync(ct);

                return checkpoint;
            },
            TimeSpan.FromMilliseconds(10),
            batchSize: 1
        );

        Task? disposing = null;

        try {
            await handler.Commit(Position(0, sequence: 0), cancellationToken);
            await inStore.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

            for (var sequence = 1UL; sequence <= 1000; sequence++) await handler.Commit(Position(sequence, sequence), cancellationToken);

            var parked = handler.Commit(Position(1001, sequence: 1001), cancellationToken).AsTask();
            await Task.Delay(200, cancellationToken);
            parked.IsCompleted.ShouldBeFalse("the commit should be parked inside the handler");

            disposing = handler.DisposeAsync().AsTask();

            (await parked.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken))
                .ShouldBeFalse("a commit released by the channel closing never queued its position, and has to say so");
        } finally {
            release.TrySetResult();

            if (disposing != null) await disposing.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        }
    }

    /// <summary>
    /// Acknowledgements reach the handler from any thread, unserialised; a lost one leaves a gap it never
    /// commits past.
    /// </summary>
    [Test]
    public async Task Concurrent_commits_all_land() {
        var (handler, committed) = Build();

        var accepted = await Task.WhenAll(
            Enumerable.Range(0, 200)
                .Select(i => Task.Run(async () => await handler.Commit(Position((ulong)i, (ulong)i), CancellationToken.None)))
        );

        accepted.ShouldAllBe(x => x, "an open handler refused an acknowledgement");

        // Commits up to the first gap, so seeing the last position means every one before it arrived too.
        var arrived = await Wait.Until(() => committed().Contains(199UL), TimeSpan.FromSeconds(5));

        arrived.ShouldBeTrue($"the handler lost an acknowledgement; it committed up to {committed().LastOrDefault()}");

        await handler.DisposeAsync();
    }

    /// <summary>
    /// Returns a snapshot delegate, not the live list — the commit worker appends from its own thread.
    /// </summary>
    static (CheckpointCommitHandler Handler, Func<List<ulong>> Committed) Build() {
        var committed = new List<ulong>();

        var handler = new CheckpointCommitHandler(
            "commit-handler-lifecycle-tests",
            (checkpoint, _, _) => {
                lock (committed) committed.Add(checkpoint.Position!.Value);

                return new(checkpoint);
            },
            TimeSpan.FromMilliseconds(10),
            batchSize: 1
        );

        return (handler, Snapshot);

        List<ulong> Snapshot() {
            lock (committed) return [..committed];
        }
    }

    static CommitPosition Position(ulong position, ulong sequence) => new(position, sequence, DateTime.UtcNow);

}
