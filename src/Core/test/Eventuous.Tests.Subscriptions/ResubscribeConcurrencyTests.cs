using System.Collections.Concurrent;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tools;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// One failure — a transport drop or a burst of handler nacks — must cost exactly one resubscribe,
/// with no accumulation and a checkpoint that keeps moving.
/// </summary>
public class ResubscribeConcurrencyTests {
    /// <summary>
    /// A whole page of messages fails at once, so Dropped is called once per message, all in one drop window.
    /// </summary>
    [Test]
    public async Task Burst_of_nacks_produces_a_single_resubscribe(CancellationToken ct) {
        var logs = new CapturingLoggerFactory(LogLevel.Trace);

        const int messageCount = 8;

        var handler = new DeferringHandler(_ => true);

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "burst-of-nacks",
                ThrowOnError              = true,
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(handler),
            logs,
            concurrencyLimit: 4,
            // Only the first run delivers — a replacement redelivering the same messages would open a second drop window.
            pump: async (sub, transport, start, run) => {
                if (transport.Index == 0) {
                    for (var i = 0; i < messageCount; i++) await sub.Deliver(run, start + (ulong)i).NoContext();
                }

                await Task.Delay(Timeout.Infinite, run.Token).NoContext();
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(500) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        // All of them have to fail before the resubscribe fires, or this proves nothing about concurrent drops.
        (await Wait.Until(() => handler.HandledCount >= messageCount, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue($"all {messageCount} messages should have been handled and nacked, got {handler.HandledCount}");

        (await Wait.Until(() => subscription.SubscribeCalls > 1, TimeSpan.FromSeconds(5))).ShouldBeTrue("the subscription should have resubscribed");

        // Give any extra resubscribes scheduled by the other nacks time to show up before asserting.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        await subscription.Unsubscribe(_ => { }, ct);

        subscription.SubscribeCalls.ShouldBe(2, "one initial subscribe plus exactly one resubscribe for the whole drop cycle");
        logs.Count("Resubscribing").ShouldBe(1, "a burst of nacks is one drop cycle, so it gets one 'Resubscribing' line");
        logs.Count("Dropped:").ShouldBe(1, "the drop is reported once per cycle, not once per failing message");
    }

    /// <summary>
    /// No handler involvement at all: the connection dies, so the pump's read throws and every in-flight
    /// operation on it fails at the same moment. One transport failure, one resubscribe.
    /// </summary>
    [Test]
    public async Task Transport_failure_produces_a_single_resubscribe(CancellationToken ct) {
        const int inFlight = 4;
        const ulong last = 7;

        var logs    = new CapturingLoggerFactory(LogLevel.Trace);
        var store   = new NoOpCheckpointStore();
        var handler = new ConnectionBoundHandler(t => t == 0);

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "transport-failure",
                ThrowOnError              = true,
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            store,
            new ConsumePipe().AddDefaultConsumer(handler),
            logs,
            concurrencyLimit: inFlight,
            pump: async (sub, transport, start, run) => {
                if (transport.Index > 0) {
                    for (var i = start; i <= last && !run.Token.IsCancellationRequested; i++) await sub.Deliver(run, i, transport.Index).NoContext();

                    await Task.Delay(Timeout.Infinite, run.Token).NoContext();

                    return;
                }

                for (var i = 0; i < inFlight; i++) await sub.Deliver(run, (ulong)i, transport.Index).NoContext();

                // Wait until they're in flight, so the failure hits all of them at once.
                await Wait.Until(() => handler.InFlight == inFlight, TimeSpan.FromSeconds(5)).NoContext();

                // HTTP/2 INTERNAL_ERROR: the connection is gone, and everything using it fails together.
                handler.KillConnection(transport.Index);

                // What AllStreamSubscription.PumpMessages does when MoveNextAsync throws.
                run.Fail(DropReason.SubscriptionError, new IOException("The HTTP/2 server closed the connection"));

                await Task.Delay(Timeout.Infinite, run.Token).NoContext();
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(300) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        var recovered = await Wait.Until(
            async () => (await store.GetLastCheckpoint(subscription.SubscriptionId, ct)).Position == last,
            TimeSpan.FromSeconds(15)
        );

        // Let any resubscribe scheduled by the other nacks arrive before counting.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
        await subscription.Unsubscribe(_ => { }, ct);

        recovered.ShouldBeTrue("the subscription must commit again after the connection comes back");

        subscription.SubscribeCalls.ShouldBe(2, "one initial subscribe plus exactly one resubscribe for the whole connection failure");
        logs.Count("Resubscribing").ShouldBe(1, "the pump's drop and the in-flight nacks are one drop cycle between them");
        logs.Count("Dropped:").ShouldBe(1, "the drop is reported once per cycle, not once per failed operation");

        var transports = subscription.Transports.ToArray();
        transports[0].IsDisposed.ShouldBeTrue("the dead connection should have been disposed");
        transports[0].PumpExited.ShouldBeTrue("the pump reading the dead connection should have exited");
        subscription.MaxLivePumps.ShouldBe(1, "there should never be more than one message pump alive at a time");
    }

    /// <summary>
    /// The same failure over and over — a rolling restart, or a flapping node. Nothing may accumulate,
    /// and the checkpoint has to end up where it would have without any of it.
    /// </summary>
    [Test]
    public async Task Repeated_transport_failures_do_not_accumulate(CancellationToken ct) {
        const int cycles = 15;
        const int inFlight = 4;
        const ulong last = 7;

        var store   = new NoOpCheckpointStore();
        var handler = new ConnectionBoundHandler(t => t < cycles);

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "repeated-transport-failure",
                ThrowOnError              = true,
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            store,
            new ConsumePipe().AddDefaultConsumer(handler),
            new CapturingLoggerFactory(LogLevel.Trace),
            concurrencyLimit: inFlight,
            pump: async (sub, transport, start, run) => {
                if (transport.Index >= cycles) {
                    // The store settles down and the subscription catches up.
                    for (var i = start; i <= last && !run.Token.IsCancellationRequested; i++) await sub.Deliver(run, i, transport.Index).NoContext();

                    await Task.Delay(Timeout.Infinite, run.Token).NoContext();

                    return;
                }

                for (var i = start; i < start + inFlight && !run.Token.IsCancellationRequested; i++) await sub.Deliver(run, i, transport.Index).NoContext();

                await Wait.Until(() => handler.InFlight == inFlight, TimeSpan.FromSeconds(5)).NoContext();
                handler.KillConnection(transport.Index);

                run.Fail(DropReason.SubscriptionError, new IOException("The HTTP/2 server closed the connection"));

                await Task.Delay(Timeout.Infinite, run.Token).NoContext();
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(10) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        var recovered = await Wait.Until(
            async () => (await store.GetLastCheckpoint(subscription.SubscriptionId, ct)).Position == last,
            TimeSpan.FromSeconds(60)
        );

        await subscription.Unsubscribe(_ => { }, ct);

        recovered.ShouldBeTrue($"the checkpoint should have caught up after {cycles} connection failures");

        subscription.SubscribeCalls.ShouldBe(cycles + 1, "each connection failure should cost exactly one resubscribe");
        subscription.MaxLivePumps.ShouldBe(1, "message pumps accumulated across connection failures");
        subscription.Transports.Count(t => !t.IsDisposed).ShouldBe(0, "every dead connection should have been disposed");
        subscription.Transports.Count.ShouldBe(subscription.SubscribeCalls, "each subscribe should create exactly one connection");
    }

    /// <summary>
    /// The dropped run's pump must be gone before the next one starts. An orphan keeps reading into the
    /// same consume pipe, and every message it delivers is a duplicate.
    /// </summary>
    [Test]
    public async Task Resubscribe_stops_the_previous_transport_and_pump(CancellationToken ct) {
        var deliveries = new ConcurrentQueue<(int Transport, ulong Position)>();

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "stop-previous-pump",
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(new CountingHandler()),
            new CapturingLoggerFactory(LogLevel.Trace),
            concurrencyLimit: 1,
            pump: async (sub, transport, start, run) => {
                var position = start;

                while (!run.Token.IsCancellationRequested) {
                    deliveries.Enqueue((transport.Index, position));
                    await sub.Deliver(run, position++).NoContext();
                    await Task.Delay(5, run.Token).NoContext();
                }
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(100) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        (await Wait.Until(() => deliveries.Count > 3, TimeSpan.FromSeconds(5))).ShouldBeTrue("the first pump should be delivering");

        subscription.Drop(new InvalidOperationException("Simulated transport drop"));

        (await Wait.Until(() => deliveries.Any(d => d.Transport == 1), TimeSpan.FromSeconds(5))).ShouldBeTrue("the replacement pump should be delivering");

        // Let the replacement get well clear of the switch, so the old pump has every chance to interleave.
        (await Wait.Until(() => deliveries.Count(d => d.Transport == 1) >= 3, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the replacement pump should have kept delivering");

        await subscription.Unsubscribe(_ => { }, ct);

        var transports = subscription.Transports.ToArray();
        transports.Length.ShouldBe(2);

        transports[0].IsDisposed.ShouldBeTrue("the dropped transport should have been disposed before the new one was created");
        transports[0].PumpExited.ShouldBeTrue("the dropped run's message pump should have exited");

        // Once the replacement pump has delivered anything, the old one must never be heard from again.
        var recorded    = deliveries.ToArray();
        var firstOnNew  = Array.FindIndex(recorded, d => d.Transport == 1);
        var afterSwitch = recorded.Skip(firstOnNew);

        afterSwitch.ShouldAllBe(d => d.Transport == 1, "two live pumps were dispatching into the same pipe");
        subscription.MaxLivePumps.ShouldBe(1, "there should never be more than one message pump alive at a time");
    }

    /// <summary>
    /// A transport that can't reconnect — the broker is still down — must keep being retried, not give up.
    /// </summary>
    [Test]
    public async Task A_resubscribe_that_fails_to_connect_keeps_retrying(CancellationToken ct) {
        var logs = new CapturingLoggerFactory(LogLevel.Trace);

        const int failUntil = 4;

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "resubscribe-retries",
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(new CountingHandler()),
            logs,
            concurrencyLimit: 1,
            pump: (_, _, _, run) => Task.Delay(Timeout.Infinite, run.Token)
        ) {
            ResubscribeDelay = TimeSpan.FromMilliseconds(50),
            // Attempt 0 is the original run coming up; the broker stays down through attempt failUntil.
            FailStart = attempt => attempt is > 0 and < failUntil ? new InvalidOperationException($"Connection refused on attempt {attempt}") : null
        };

        var transitions = new Transitions();

        await subscription.Subscribe(_ => transitions.Subscribed(), (_, _, _) => transitions.Dropped(), ct);

        subscription.Drop(new InvalidOperationException("Simulated transport drop"));

        // Wait on the attempt count, not a transition: a loop that gives up leaves no cycle open either.
        var retried = await Wait.Until(() => subscription.SubscribeCalls > failUntil, TimeSpan.FromSeconds(15));

        (await Wait.Until(() => transitions.Up, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the subscription should have come back up once the broker stopped refusing");

        // Ten retry delays: long enough that a loop still running would have spent another attempt, which the
        // exact count below would catch.
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);

        var attempts = subscription.SubscribeCalls;
        var up       = transitions.Up;

        await subscription.Unsubscribe(_ => { }, ct);

        retried.ShouldBeTrue($"the subscription stopped retrying after {attempts} attempts and never came back up");
        attempts.ShouldBe(failUntil + 1, "the loop kept resubscribing after the replacement run was up");
        up.ShouldBeTrue("the drop cycle outlived the run that recovered from it");
    }

    /// <summary>
    /// A drop landing while the replacement run is still coming up must not cost an extra attempt of its own.
    /// </summary>
    /// <remarks>
    /// Not a retry-delay test: the supervisor is inside Connect for the whole hold, so the delay is
    /// irrelevant here. That's covered by <c>SupervisorTests.The_retry_delay_elapses_between_connect_attempts</c>.
    /// </remarks>
    [Test]
    public async Task Drop_while_a_resubscribe_is_in_flight_does_not_spin(CancellationToken ct) {
        var logs = new CapturingLoggerFactory(LogLevel.Trace);

        var reached  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starting = 0;

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "drop-during-resubscribe",
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(new CountingHandler()),
            logs,
            concurrencyLimit: 1,
            pump: (_, _, _, run) => Task.Delay(Timeout.Infinite, run.Token)
        ) {
            ResubscribeDelay = TimeSpan.FromMilliseconds(50),
            // Only the replacement run is held; the first has to come up for there to be one to drop.
            WhileStarting = async () => {
                if (Interlocked.Increment(ref starting) != 2) return;

                reached.TrySetResult();
                await release.Task;
            }
        };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        subscription.Drop(new InvalidOperationException("Simulated transport drop"));

        (await Wait.Until(() => reached.Task.IsCompleted, TimeSpan.FromSeconds(5))).ShouldBeTrue("the resubscribe should have reached the replacement run");

        // Opens a second drop cycle while the first resubscribe still holds the lifecycle.
        subscription.Drop(new InvalidOperationException("Simulated drop on the replacement"));

        // The supervisor is parked inside Connect, so no further attempt can start however many drops land.
        logs.Count("Resubscribing").ShouldBe(1, "a drop landing mid-connect must not start an attempt alongside the one in flight");

        release.SetResult();

        // The held attempt finishes, and only then is the queued drop handled — one further cycle, not a spin.
        (await Wait.Until(() => Volatile.Read(ref starting) >= 3, TimeSpan.FromSeconds(10)))
            .ShouldBeTrue($"the drop taken during the hold should have been served after it, only saw {Volatile.Read(ref starting)} connect(s)");

        await subscription.Unsubscribe(_ => { }, ct);

        logs.Count("Resubscribing").ShouldBe(2, "the drop that landed mid-connect costs exactly one further cycle; spinning produces thousands");
    }

    /// <summary>
    /// Two contexts sharing a sequence number collapse into one entry in the commit handler's position set,
    /// which then refuses to commit past the resulting hole.
    /// </summary>
    /// <remarks>
    /// A contract guard, not a race detector: the counter is an unconditional Interlocked.Increment, so this
    /// can't find a bad interleaving — non-atomicity would have to be caught by review, not by running this.
    /// </remarks>
    [Test]
    public async Task Concurrent_context_creation_yields_unique_sequences(CancellationToken ct) {
        const int threads = 8;
        const int perThread = 2000;

        var subscription = new PumpingSubscription(
            new() { SubscriptionId = "unique-sequences" },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(new CountingHandler()),
            new CapturingLoggerFactory(LogLevel.Trace),
            concurrencyLimit: 1,
            pump: (_, _, _, run) => Task.Delay(Timeout.Infinite, run.Token)
        );

        // The counter belongs to the run, so there has to be one to draw from.
        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        var run     = subscription.Run.ShouldNotBeNull();
        var results = new ulong[threads][];

        await Task.WhenAll(
            Enumerable.Range(0, threads)
                .Select(t => Task.Run(() => {
                            var mine = new ulong[perThread];

                            for (var i = 0; i < perThread; i++) mine[i] = run.NextSequence();

                            results[t] = mine;
                        }
                    )
                )
        );

        await subscription.Unsubscribe(_ => { }, ct);

        var all = results.SelectMany(x => x).ToArray();

        all.Length.ShouldBe(threads * perThread);
        all.Distinct().Count().ShouldBe(all.Length, "sequence numbers must be unique across concurrent context creation");
        all.Order().ShouldBe(Enumerable.Range(0, all.Length).Select(i => (ulong)i), "sequence numbers must be a gapless monotonic run");

        // Each thread must see its own values increase, so the sequence is monotonic per producer too.
        foreach (var mine in results) mine.ShouldBeInOrder(SortDirection.Ascending);
    }

    /// <summary>
    /// The steady state of a deferring handler: the same message fails on every redelivery, the checkpoint
    /// holds just before it, and nothing accumulates.
    /// </summary>
    [Test]
    public async Task Repeated_nacks_hold_a_stable_checkpoint_without_accumulating(CancellationToken ct) {
        // Enough cycles that anything per-cycle would be plainly visible in the bounds below; a hundred cost
        // minutes of CI and proved nothing thirty don't.
        const int cycles = 30;
        const ulong failAt = 3;

        var stored = new ConcurrentQueue<ulong?>();
        var store  = new NoOpCheckpointStore();
        store.CheckpointStored += (_, cp) => stored.Enqueue(cp.Position);

        var handler = new DeferringHandler(context => context.GlobalPosition >= failAt);

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "stable-checkpoint",
                ThrowOnError              = true,
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            store,
            new ConsumePipe().AddDefaultConsumer(handler),
            new CapturingLoggerFactory(LogLevel.Trace),
            concurrencyLimit: 1,
            pump: async (sub, _, start, run) => {
                for (var i = start; i < start + 5 && !run.Token.IsCancellationRequested; i++) await sub.Deliver(run, i).NoContext();

                await Task.Delay(Timeout.Infinite, run.Token).NoContext();
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(10) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        (await Wait.Until(() => subscription.SubscribeCalls > cycles, TimeSpan.FromSeconds(60)))
            .ShouldBeTrue($"the subscription should have gone through {cycles} drop cycles, it did {subscription.SubscribeCalls - 1}");

        await subscription.Unsubscribe(_ => { }, ct);

        // A corrupted sequence would show up as a checkpoint that never commits, or one that jumps past the failure.
        stored.ShouldNotBeEmpty("the messages before the failing one should have been committed");
        stored.ShouldAllBe(p => p < failAt, "the checkpoint must never advance past the message that keeps failing");
        stored.Last().ShouldBe(failAt - 1, "the checkpoint should settle on the last message before the failing one");
        (await store.GetLastCheckpoint(subscription.SubscriptionId, ct)).Position.ShouldBe(failAt - 1);

        // Plus one for the flush when the first commit handler is replaced.
        stored.Count.ShouldBeLessThanOrEqualTo((int)failAt + 1, $"the checkpoint should settle, not be rewritten across {cycles} cycles");

        handler.HandledCount.ShouldBeGreaterThanOrEqualTo(cycles, "the failing message must be redelivered on every cycle");

        subscription.MaxLivePumps.ShouldBe(1, "message pumps accumulated across drop cycles");
        subscription.Transports.Count(t => !t.IsDisposed).ShouldBe(0, "every transport should have been disposed by the time the subscription stops");
        subscription.Transports.Count.ShouldBe(subscription.SubscribeCalls, "each subscribe should create exactly one transport");
    }

    /// <summary>
    /// A subscription whose checkpoint stalled on a gap needs a clean commit handler from the next
    /// subscribe, or it processes forever without ever committing.
    /// </summary>
    [Test]
    public async Task Subscription_commits_again_after_a_commit_gap(CancellationToken ct) {
        const ulong failAt = 3;
        const ulong last = 7;

        var store = new NoOpCheckpointStore();
        var defer = true;

        // Fails on one message until the test lets it through, exactly like a precondition that resolves.
        var handler = new DeferringHandler(context => defer && context.GlobalPosition == failAt);

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "recover-after-gap",
                ThrowOnError              = true,
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            store,
            new ConsumePipe().AddDefaultConsumer(handler),
            new CapturingLoggerFactory(LogLevel.Trace),
            concurrencyLimit: 1,
            pump: async (sub, _, start, run) => {
                for (var i = start; i <= last && !run.Token.IsCancellationRequested; i++) await sub.Deliver(run, i).NoContext();

                await Task.Delay(Timeout.Infinite, run.Token).NoContext();
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(50) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        // The messages after the failing one ack out of order, leaving a hole nothing commits past.
        var stalled = await Wait.Until(
            async () => (await store.GetLastCheckpoint(subscription.SubscriptionId, ct)).Position == failAt - 1,
            TimeSpan.FromSeconds(10)
        );

        stalled.ShouldBeTrue("the checkpoint should have stalled just before the failing message");

        // The precondition resolves. Nothing else changes — no restart, no manual intervention.
        defer = false;

        var recovered = await Wait.Until(
            async () => (await store.GetLastCheckpoint(subscription.SubscriptionId, ct)).Position == last,
            TimeSpan.FromSeconds(15)
        );

        await subscription.Unsubscribe(_ => { }, ct);

        recovered.ShouldBeTrue("a subscription whose checkpoint stalled must commit again once the failing message succeeds");
    }


    /// <summary>
    /// An acknowledgement must commit through the run that dispatched the message, never through whichever
    /// run is current — otherwise it can collide with or paper over a hole in a different run's sequence.
    /// </summary>
    /// <remarks>
    /// Not a contrived window: teardown only joins the transport pump, so a message still inside a handler
    /// when its run ends is the ordinary case on every resubscribe.
    /// </remarks>
    [Test]
    public async Task An_acknowledgement_from_a_dropped_run_is_refused(CancellationToken ct) {
        var logs    = new CapturingLoggerFactory(LogLevel.Trace);
        var handler = new ParkingHandler();

        var subscription = new PumpingSubscription(
            new() { SubscriptionId = "ack-belongs-to-its-run", CheckpointCommitBatchSize = 1, CheckpointCommitDelayMs = 10 },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(handler),
            logs,
            concurrencyLimit: 1,
            pump: async (sub, transport, _, run) => {
                // Only the first run delivers; the replacement just holds its connection open.
                if (transport.Index == 0) await sub.Deliver(run, 0).NoContext();

                await run.Ended.NoContext();
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(50) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        try {
            (await handler.Parked.WaitAsync(TimeSpan.FromSeconds(5), ct).ContinueWith(t => t.IsCompletedSuccessfully, ct))
                .ShouldBeTrue("the handler should be holding the first run's message");

            subscription.Drop(new IOException("the connection died while a handler was mid-flight"));

            (await Wait.Until(() => subscription.SubscribeCalls >= 2, TimeSpan.FromSeconds(10)))
                .ShouldBeTrue("the replacement run should have come up while the handler was still parked");

            handler.Release();

            (await Wait.Until(() => logs.Count("belongs to a previous run") >= 1, TimeSpan.FromSeconds(10)))
                .ShouldBeTrue("the late acknowledgement should have been refused by the run that dispatched it");
        } finally {
            handler.Release();

            // The test's own token, not None — an unbounded wait in a finally turns a failing test into a hanging one.
            await subscription.Unsubscribe(_ => { }, ct);
        }
    }

    /// <summary>
    /// The positive counterpart to the test above: an acknowledgement that lands while its own run is being
    /// torn down must still commit. That is what the release ordering buys — the commit handler is registered
    /// before Connect, so it releases after every transport handle, and a handler still mid-flight when
    /// teardown starts gets its checkpoint written rather than dropped.
    /// </summary>
    /// <remarks>
    /// Reversing that order turns this into silent checkpoint loss: the ack is refused, the subscription
    /// still stops cleanly, and the work is replayed on the next start with nothing logged as an error.
    /// </remarks>
    [Test]
    [Timeout(30_000)]
    public async Task An_acknowledgement_in_flight_during_teardown_still_commits(CancellationToken ct) {
        var logs    = new CapturingLoggerFactory(LogLevel.Trace);
        var handler = new ParkingHandler();
        var store   = new NoOpCheckpointStore();

        var stored = new TaskCompletionSource<ulong?>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.CheckpointStored += (_, checkpoint) => stored.TrySetResult(checkpoint.Position);

        var subscription = new PumpingSubscription(
            new() { SubscriptionId = "ack-during-teardown", CheckpointCommitBatchSize = 1, CheckpointCommitDelayMs = 10 },
            store,
            new ConsumePipe().AddDefaultConsumer(handler),
            logs,
            concurrencyLimit: 1,
            pump: async (sub, _, _, run) => {
                await sub.Deliver(run, 0).NoContext();
                await run.Ended.NoContext();
            }
        ) {
            ResubscribeDelay = TimeSpan.FromMilliseconds(50),
            // Teardown has started and the transport is going away, but the commit handler is still open.
            WhileStopping = async () => {
                handler.Release();

                // Bounded, so a refused ack fails the assertion below instead of hanging teardown forever.
                await Task.WhenAny(stored.Task, Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None));
            }
        };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        try {
            await handler.Parked.WaitAsync(TimeSpan.FromSeconds(10), ct);

            // A clean stop is the same teardown a resubscribe runs, so this covers both.
            await subscription.Unsubscribe(_ => { }, ct);
        } finally {
            handler.Release();
        }

        stored.Task.IsCompletedSuccessfully.ShouldBeTrue("an ack raised while the run was tearing down must reach the commit handler that dispatched it");
        (await stored.Task).ShouldBe(0ul, "the position the parked handler acknowledged is the one that must be durable");

        logs.Count("belongs to a previous run").ShouldBe(0, "the run that dispatched the message was still the one acknowledging it");
    }


    const string TransportKey = "transport";

    record TestOptions : SubscriptionWithCheckpointOptions;

    /// <summary>
    /// Stands in for a transport connection, tracking disposal and whether its pump has exited.
    /// </summary>
    sealed class FakeTransport(int index) : IAsyncDisposable {
        public int  Index      { get; } = index;
        public bool IsDisposed { get; private set; }
        public bool PumpExited { get; set; }

        public ValueTask DisposeAsync() {
            IsDisposed = true;

            return default;
        }
    }

    /// <summary>
    /// Shaped like the real catch-up subscriptions: Connect creates a transport, a pump reads it, Disconnect
    /// drops it. Pump body is supplied per test.
    /// </summary>
    sealed class PumpingSubscription(
            TestOptions                                                             options,
            ICheckpointStore                                                        checkpointStore,
            ConsumePipe                                                             pipe,
            ILoggerFactory?                                                         loggerFactory,
            int                                                                     concurrencyLimit,
            Func<PumpingSubscription, FakeTransport, ulong, SubscriptionRun, Task>  pump
        )
        : EventSubscriptionWithCheckpoint<TestOptions>(options, checkpointStore, pipe, concurrencyLimit, SubscriptionKind.All, loggerFactory, null, null) {
        readonly ConcurrentQueue<FakeTransport> _transports = [];

        SubscriptionRun? _run;
        int              _subscribeCalls;
        int              _livePumps;
        int              _maxLivePumps;

        public IReadOnlyCollection<FakeTransport> Transports     => _transports;
        public int                                SubscribeCalls => Volatile.Read(ref _subscribeCalls);
        public int                                MaxLivePumps   => Volatile.Read(ref _maxLivePumps);

        /// <summary>
        /// The newest run this subscription was given, for drawing sequence numbers or failing from outside the loop.
        /// </summary>
        public SubscriptionRun? Run => Volatile.Read(ref _run);

        /// <summary>
        /// Writes through to the option the supervisor reads. The 2s default would put a hundred cycles at
        /// three minutes against a 60s budget, so every test here sets its own.
        /// </summary>
        public TimeSpan ResubscribeDelay {
            init => Options.RetryDelay = value;
        }

        /// <summary>
        /// Awaited part-way through bringing a run up, so a test can hold a restart open and see what a
        /// drop arriving in that window costs.
        /// </summary>
        public Func<Task>? WhileStarting { get; init; }

        /// <summary>
        /// Awaited inside the transport's own release, so a test can act while teardown is underway but the
        /// commit handler — which registers first and so releases last — is still open. That window is where
        /// a late acknowledgement has to land.
        /// </summary>
        public Func<Task>? WhileStopping { get; init; }

        /// <summary>
        /// Consulted with the attempt number before a run is brought up, so a test can make a transport
        /// refuse to connect and leave recovery entirely to the resubscribe loop.
        /// </summary>
        public Func<int, Exception?>? FailStart { get; init; }

        /// <summary>
        /// Fails the current run, standing in for a transport reporting from outside the loop.
        /// </summary>
        public void Drop(Exception exception) => Run?.Fail(DropReason.SubscriptionError, exception);

        public MessageConsumeContext CreateContext(SubscriptionRun run, ulong position, int transport = 0)
            => new MessageConsumeContext(
                    Guid.NewGuid().ToString(),
                    "TestEvent",
                    "application/json",
                    "test-stream",
                    position,
                    position,
                    position,
                    run.NextSequence(),
                    DateTime.UtcNow,
                    new { Position = position },
                    new(),
                    Options.SubscriptionId,
                    CancellationToken.None
                ) { LogContext = Log }
                .WithItem(TransportKey, transport);

        public ValueTask Deliver(SubscriptionRun run, ulong position, int transport = 0) {
            var context = CreateContext(run, position, transport);
            context.CancellationToken = run.Token;

            return HandleInternal(run, context);
        }

        protected override async ValueTask Connect(SubscriptionRun run) {
            // First, so a drop arriving mid-connect finds the run it belongs to (see WhileStarting tests).
            Volatile.Write(ref _run, run);

            var (_, position) = await GetCheckpoint(run).NoContext();
            var start         = position == null ? 0 : position.Value + 1;

            var attempt = Interlocked.Increment(ref _subscribeCalls) - 1;

            if (FailStart?.Invoke(attempt) is { } failure) throw failure;

            var transport = new FakeTransport(attempt);
            _transports.Enqueue(transport);

            if (WhileStarting != null) await WhileStarting().NoContext();

            // Started on a task of its own so it never runs inline on the supervisor's stack during Connect.
            var pumping = Task.Run(() => Pump(run, transport, start), CancellationToken.None);

            // Dispose then join, same order teardown always used, just one release instead of two steps.
            run.OnDisconnect(async _ => {
                if (WhileStopping != null) await WhileStopping().NoContext();

                await transport.DisposeAsync().NoContext();
                await pumping.NoContext();
            });
        }

        /// <summary>
        /// Runs on a task of its own, so <see cref="_maxLivePumps"/> is a real measurement of overlap. Reports
        /// its own death — a plain return or exception while <paramref name="run"/> is still live is a drop
        /// nothing else would otherwise notice.
        /// </summary>
        async Task Pump(SubscriptionRun run, FakeTransport transport, ulong start) {
            TrackPumpStarted();

            try {
                await TransportPump.Run(run, () => pump(this, transport, start, run), "PumpingSubscription pump ended while the connection was up").NoContext();
            } finally {
                Interlocked.Decrement(ref _livePumps);
                transport.PumpExited = true;
            }
        }

        void TrackPumpStarted() {
            var live = Interlocked.Increment(ref _livePumps);

            int observed;

            do {
                observed = Volatile.Read(ref _maxLivePumps);

                if (observed >= live) return;
            } while (Interlocked.CompareExchange(ref _maxLivePumps, live, observed) != observed);
        }
    }

    /// <summary>
    /// Defers by throwing when a precondition isn't met, so the message is redelivered after the resubscribe.
    /// </summary>
    sealed class DeferringHandler(Func<IMessageConsumeContext, bool> shouldDefer) : BaseEventHandler {
        int _handled;

        public int HandledCount => Volatile.Read(ref _handled);

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            if (!shouldDefer(context)) return new(EventHandlingStatus.Success);

            Interlocked.Increment(ref _handled);

            throw new InvalidOperationException($"Precondition not met for {context.Stream}:{context.GlobalPosition}");
        }
    }

    /// <summary>
    /// Does I/O over the subscription's own connection, so a connection death turns into a batch of
    /// simultaneous nacks.
    /// </summary>
    sealed class ConnectionBoundHandler(Func<int, bool> holdsUntilConnectionDies) : BaseEventHandler {
        readonly ConcurrentDictionary<int, TaskCompletionSource> _dead = [];

        int _inFlight;

        public int InFlight => Volatile.Read(ref _inFlight);

        public void KillConnection(int transport) => Killed(transport).TrySetResult();

        TaskCompletionSource Killed(int transport) => _dead.GetOrAdd(transport, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));

        public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            var transport = context.Items.GetItem<int>(TransportKey);
            var killed    = Killed(transport).Task;

            if (killed.IsCompleted) throw Dead(transport);

            if (!holdsUntilConnectionDies(transport)) return EventHandlingStatus.Success;

            // Hold the operation open, so the failure catches the whole batch at once.
            Interlocked.Increment(ref _inFlight);

            try { await killed.WaitAsync(context.CancellationToken).NoContext(); } finally { Interlocked.Decrement(ref _inFlight); }

            throw Dead(transport);
        }

        static IOException Dead(int transport) => new($"The HTTP/2 server closed the connection (transport {transport})");
    }

    sealed class CountingHandler : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => new(EventHandlingStatus.Success);
    }

    /// <summary>
    /// Holds the first message until released, so its acknowledgement lands after the dispatching run is
    /// already torn down.
    /// </summary>
    sealed class ParkingHandler : BaseEventHandler {
        readonly TaskCompletionSource _parked  = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        int _parkedOnce;

        public Task Parked => _parked.Task;

        public void Release() => _release.TrySetResult();

        public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            if (Interlocked.CompareExchange(ref _parkedOnce, 1, 0) != 0) return EventHandlingStatus.Success;

            _parked.TrySetResult();

            // Deliberately not watching the context token, or this would abandon the message instead of acking it late.
            await _release.Task;

            return EventHandlingStatus.Success;
        }
    }

}
