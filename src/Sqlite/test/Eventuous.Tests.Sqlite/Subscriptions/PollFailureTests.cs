// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sqlite.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Data.Sqlite;
using Shouldly;

namespace Eventuous.Tests.Sqlite.Subscriptions;

/// <summary>
/// How <c>SqlSubscriptionBase.PollOnce</c> classifies a failed poll — stop, retry, or drop. Sqlite shares that
/// base with Postgres and SQL Server but needs no container, so it is the cheapest place to pin the
/// classification, and the only one that runs where SQL Server's suite is excluded.
/// </summary>
/// <remarks>Every test fails at the <c>OpenConnection</c> seam, so no database is involved.</remarks>
public class PollFailureTests {
    /// <summary>
    /// A transient failure is the loop's own business. Escalating it would turn a blip into a full reconnect.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task A_transient_failure_is_retried_without_reporting_a_drop(CancellationToken ct) {
        var subscription = new PollingSubscription("sqlite-transient", (_, _) => throw new TimeoutException("the database was busy"), transient: true);

        var drops = 0;
        await subscription.Subscribe(_ => { }, (_, _, _) => Interlocked.Increment(ref drops), ct);

        var retried = await WaitUntil(() => subscription.Polls > 3, TimeSpan.FromSeconds(10));

        await subscription.Unsubscribe(_ => { }, ct);

        retried.ShouldBeTrue($"a transient failure should be retried inside the poll loop, it polled {subscription.Polls} time(s)");
        drops.ShouldBe(0, "a retry the loop handles itself must not be reported as a dropped subscription");
    }

    /// <summary>
    /// Anything not transient propagates out of the loop, which the pump reads as this run's failure. Without
    /// it the subscription stops consuming while still reporting healthy.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task A_fatal_poll_failure_is_reported_as_a_drop(CancellationToken ct) {
        var failure      = new InvalidOperationException("no such table: eventuous.messages");
        var subscription = new PollingSubscription("sqlite-fatal", (_, _) => throw failure);

        DropReason? reason    = null;
        Exception?  reported  = null;

        await subscription.Subscribe(_ => { }, (_, r, e) => { reason = r; reported = e; }, ct);

        var dropped = await WaitUntil(() => reason != null, TimeSpan.FromSeconds(10));

        await subscription.Unsubscribe(_ => { }, ct);

        dropped.ShouldBeTrue("a poll failure the provider can't retry has to reach the dropped callback, so health checks see it");
        reason.ShouldBe(DropReason.ServerError);
        reported.ShouldBeSameAs(failure, "the cause must survive the trip out of the poll loop");
    }

    /// <summary>
    /// The regression the token check guards: providers report unrelated aborts with a cancellation shape
    /// (SQL Server's "Operation cancelled by user."), so one raised while nobody asked to stop is a real fault.
    /// </summary>
    /// <remarks>
    /// Trusting the shape alone ends the loop silently — the pump returns, nothing is recorded, and the
    /// subscription reports healthy while consuming nothing.
    /// </remarks>
    [Test]
    [Timeout(30_000)]
    public async Task A_cancellation_shaped_failure_with_no_stop_requested_is_still_a_drop(CancellationToken ct) {
        var subscription = new PollingSubscription("sqlite-foreign-cancel", (_, _) => throw new OperationCanceledException("Operation cancelled by user."));

        DropReason? reason = null;
        await subscription.Subscribe(_ => { }, (_, r, _) => reason = r, ct);

        var dropped = await WaitUntil(() => reason != null, TimeSpan.FromSeconds(10));

        await subscription.Unsubscribe(_ => { }, ct);

        dropped.ShouldBeTrue("a cancellation nobody asked for is a fault, and swallowing it leaves a silently dead subscription");
        reason.ShouldBe(DropReason.ServerError);
    }

    /// <summary>
    /// The other side of the check: once the run's token is cancelled, the poll's cancellation is our own
    /// shutdown, not a drop.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task A_cancellation_during_shutdown_is_not_reported_as_a_drop(CancellationToken ct) {
        // Parks in the poll until the run token cancels, which is what a real read does during a clean stop.
        var subscription = new PollingSubscription("sqlite-clean-stop", async (_, token) => await Task.Delay(Timeout.Infinite, token));

        var drops = 0;
        await subscription.Subscribe(_ => { }, (_, _, _) => Interlocked.Increment(ref drops), ct);

        (await WaitUntil(() => subscription.Polls >= 1, TimeSpan.FromSeconds(10))).ShouldBeTrue("the poll loop should have started");

        await subscription.Unsubscribe(_ => { }, ct);

        drops.ShouldBe(0, "stopping is not dropping — a cancellation we asked for describes the shutdown");
    }

    static async Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline) {
            if (condition()) return true;

            await Task.Delay(20);
        }

        return condition();
    }

    /// <summary>
    /// Replaces the connection with whatever the test wants a poll to do. The connection string is required by
    /// the base constructor but never used.
    /// </summary>
    sealed class PollingSubscription(string id, Func<int, CancellationToken, Task> onPoll, bool transient = false)
        : SqliteAllStreamSubscription(
            new() {
                SubscriptionId   = id,
                ConnectionString = "Data Source=:memory:",
                // Short, so a retrying loop churns through attempts rather than sitting in backoff.
                Retry            = new() { InitialDelayMs = 1 },
                Polling          = new() { MinIntervalMs = 1, MaxIntervalMs = 5 },
                RetryDelay       = TimeSpan.FromMilliseconds(50)
            },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(new NoOpHandler())
        ) {
        int _polls;

        public int Polls => Volatile.Read(ref _polls);

        protected override async ValueTask<SqliteConnection> OpenConnection(CancellationToken cancellationToken) {
            await onPoll(Interlocked.Increment(ref _polls), cancellationToken).ConfigureAwait(false);

            throw new InvalidOperationException("a poll that was meant to fail returned instead");
        }

        protected override bool IsTransient(Exception exception) => transient;
    }

    sealed class NoOpHandler : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => new(EventHandlingStatus.Success);
    }
}
