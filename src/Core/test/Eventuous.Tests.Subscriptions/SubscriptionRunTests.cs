// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Logging;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// A single run's teardown contract, tested directly: a fake transport with one handle can't tell a correct
/// release ordering from a reversed one, and that ordering is what checkpoint durability rests on.
/// </summary>
public class SubscriptionRunTests {
    static LogContext Log => Logger.CreateContext("subscription-run-tests", null);

    /// <summary>
    /// Registration order is acquisition order, so releasing forwards would close the connection an in-flight
    /// ack still needs. It is why the commit handler, registered before Connect, outlives every transport handle.
    /// </summary>
    [Test]
    public async Task Releases_run_in_reverse_registration_order() {
        var order = new ConcurrentQueue<int>();
        var run   = new SubscriptionRun(CancellationToken.None);

        for (var i = 0; i < 3; i++) {
            var registered = i;
            run.OnDisconnect(_ => { order.Enqueue(registered); return default; });
        }

        await run.Stop(CancellationToken.None, Log);

        order.ToArray().ShouldBe([2, 1, 0], "first registered must release last, or an ack lands after the handle it needs is gone");
    }

    /// <summary>
    /// One handle that won't let go must not strand the rest — those are the ones holding the connection open.
    /// </summary>
    [Test]
    public async Task A_release_that_throws_does_not_strand_the_others() {
        var order = new ConcurrentQueue<int>();
        var run   = new SubscriptionRun(CancellationToken.None);

        run.OnDisconnect(_ => { order.Enqueue(0); return default; });

        // Throws synchronously, before returning a ValueTask, which is why Disconnect invokes inside the try.
        run.OnDisconnect(_ => throw new InvalidOperationException("cannot let go"));

        run.OnDisconnect(_ => { order.Enqueue(2); return default; });

        await Should.NotThrowAsync(async () => await run.Stop(CancellationToken.None, Log));

        order.ToArray().ShouldBe([2, 0], "the release registered before the throwing one still has to run");
    }

    /// <summary>
    /// The graceful token means "stop being graceful", never "stop": a release that ignores it is still awaited,
    /// or the next run reads a checkpoint the previous one hadn't finished writing.
    /// </summary>
    [Test]
    [Timeout(10_000)]
    public async Task Teardown_waits_for_a_release_that_ignores_the_graceful_token(CancellationToken ct) {
        var entered  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var letGo    = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = false;

        var run = new SubscriptionRun(CancellationToken.None);

        run.OnDisconnect(async _ => {
            entered.TrySetResult();
            await letGo.Task;
            released = true;
        });

        // Gone before teardown starts: the worst case for a release that ignores it.
        using var expired = new CancellationTokenSource();
        await expired.CancelAsync();

        var stopping = run.Stop(expired.Token, Log).AsTask();
        await entered.Task.WaitAsync(ct);

        stopping.IsCompleted.ShouldBeFalse("an expired budget must not cut a release off part-way");

        letGo.TrySetResult();
        await stopping;

        released.ShouldBeTrue("every release is awaited to completion regardless of the graceful token");
    }

    /// <summary>
    /// Teardown clears its registrations, so a second Stop releases nothing again and doesn't throw on the
    /// source the first disposed.
    /// </summary>
    [Test]
    [Timeout(10_000)]
    public async Task A_second_Stop_releases_nothing_again_and_does_not_throw(CancellationToken ct) {
        var releases = 0;
        var run      = new SubscriptionRun(CancellationToken.None);

        run.OnDisconnect(_ => { Interlocked.Increment(ref releases); return default; });

        await run.Stop(CancellationToken.None, Log);
        await Should.NotThrowAsync(async () => await run.Stop(CancellationToken.None, Log));

        releases.ShouldBe(1, "releasing a handle twice is the double-dispose the registration clear exists to prevent");
    }

    /// <summary>
    /// First reason wins: a dying transport raises several failures, and only the first is the cause.
    /// </summary>
    [Test]
    public void Fail_keeps_the_first_reason_and_ignores_later_ones() {
        var run   = new SubscriptionRun(CancellationToken.None);
        var cause = new InvalidOperationException("the connection went away");

        run.Fail(DropReason.ServerError, cause);
        run.Fail(DropReason.SubscriptionError, new FormatException("a consequence of the above"));

        run.Failure!.Reason.ShouldBe(DropReason.ServerError);
        run.Failure!.Exception.ShouldBeSameAs(cause);
    }

    /// <summary>
    /// Failure and shutdown arrive through one arm, so the supervisor awaits one signal rather than racing two.
    /// </summary>
    [Test]
    [Timeout(10_000)]
    public async Task Ended_completes_on_a_failure_and_on_cancellation_alike() {
        var failed = new SubscriptionRun(CancellationToken.None);
        failed.Ended.IsCompleted.ShouldBeFalse("a healthy run has not ended");

        failed.Fail(DropReason.ServerError, new InvalidOperationException("gone"));
        await failed.Ended;

        using var lifetime = new CancellationTokenSource();
        var       stopped  = new SubscriptionRun(lifetime.Token);

        await lifetime.CancelAsync();
        await stopped.Ended;

        stopped.Failure.ShouldBeNull("a shutdown is not a failure, so there is nothing for the supervisor to report");
    }

    /// <summary>
    /// The sequence belongs to the run, so a replacement starts from zero rather than inheriting a counter a
    /// late ack could collide with.
    /// </summary>
    [Test]
    public void NextSequence_starts_at_zero_and_each_run_has_its_own() {
        var first = new SubscriptionRun(CancellationToken.None);

        first.NextSequence().ShouldBe(0ul);
        first.NextSequence().ShouldBe(1ul);

        new SubscriptionRun(CancellationToken.None).NextSequence().ShouldBe(0ul, "a replacement run must not inherit its predecessor's sequence");
    }

    /// <summary>
    /// Drawn from the channel worker's threads, so the draw has to be atomic. Can't prove the absence of a
    /// race, but a lost update shows up as a duplicate.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task Concurrent_NextSequence_yields_unique_values(CancellationToken ct) {
        const int threads   = 8;
        const int perThread = 500;

        var run   = new SubscriptionRun(CancellationToken.None);
        var drawn = new ConcurrentQueue<ulong>();

        await Task.WhenAll(
            Enumerable.Range(0, threads)
                .Select(_ => Task.Run(() => { for (var i = 0; i < perThread; i++) drawn.Enqueue(run.NextSequence()); }, ct))
        );

        drawn.Distinct().Count().ShouldBe(threads * perThread, "two messages sharing a sequence let the checkpoint advance over one of them");
    }
}
