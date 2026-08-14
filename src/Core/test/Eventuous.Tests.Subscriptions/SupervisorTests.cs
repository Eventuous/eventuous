using System.Collections.Concurrent;
using System.Diagnostics;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Guarantees of the supervisor in EventSubscription.Supervise that no other test file exercises directly.
/// Each one regressed silently once already during the resubscribe rewrite.
/// </summary>
public class SupervisorTests {
    /// <summary>
    /// A subscription whose transport is never reachable must fail Subscribe outright, not retry forever
    /// inside it, and a first connect failure must not poison the instance for a later attempt.
    /// </summary>
    [Test]
    public async Task A_first_connect_that_throws_propagates_and_is_not_retried(CancellationToken ct) {
        var subscription = new FakeSubscription(retryDelay: TimeSpan.FromMilliseconds(50)) { FailConnect = attempt => attempt == 0 ? new InvalidOperationException("no broker") : null };

        await Should.ThrowAsync<InvalidOperationException>(() => subscription.Subscribe(_ => { }, (_, _, _) => { }, ct).AsTask());

        subscription.IsRunning.ShouldBeFalse("a first connect that gave up must not be considered running");

        // Long enough that a retry would have landed if the loop kept going after the return.
        await Task.Delay(subscription.RetryDelayForTests + TimeSpan.FromMilliseconds(200), ct);
        subscription.Connects.ShouldBe(1, "a failed first connect must not be retried");

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);
        subscription.IsRunning.ShouldBeTrue("a subscription that gave up once must still be able to start later");

        await subscription.Unsubscribe(_ => { }, ct);
    }

    /// <summary>
    /// Once a subscription has been up at least once, a connect failure is just another drop cycle, not
    /// terminal like the first.
    /// </summary>
    [Test]
    public async Task A_connect_that_throws_after_the_subscription_is_up_is_retried(CancellationToken ct) {
        var transitions = new Transitions();

        var subscription = new FakeSubscription(retryDelay: TimeSpan.FromMilliseconds(20)) {
            FailConnect = attempt => attempt == 1 ? new InvalidOperationException("blip") : null
        };

        await subscription.Subscribe(_ => transitions.Subscribed(), (_, _, _) => transitions.Dropped(), ct);

        subscription.Run!.Fail(DropReason.SubscriptionError, new InvalidOperationException("connection lost"));

        (await Wait.Until(() => subscription.Connects >= 3, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue($"the failing reconnect should have been retried, only saw {subscription.Connects} connects");

        (await Wait.Until(() => subscription.IsRunning && transitions.Up, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the subscription should have come back up after the transient connect failure");

        await subscription.Unsubscribe(_ => { }, ct);

        transitions.Drops.ShouldBeGreaterThanOrEqualTo(1, "the failing reconnect attempt should also have been reported as a drop");
    }

    /// <summary>
    /// A throwing OnSubscribed must not read as a dropped connection — it's a caller callback, not part of the handshake.
    /// </summary>
    [Test]
    public async Task A_throwing_OnSubscribed_does_not_end_the_run(CancellationToken ct) {
        var subscription = new FakeSubscription(retryDelay: TimeSpan.FromMilliseconds(50));

        await subscription.Subscribe(_ => throw new InvalidOperationException("boom from OnSubscribed"), (_, _, _) => { }, ct);

        subscription.IsRunning.ShouldBeTrue("Subscribe should still have completed the handshake");

        await Task.Delay(subscription.RetryDelayForTests + TimeSpan.FromMilliseconds(200), ct);

        subscription.Connects.ShouldBe(1, "a throwing OnSubscribed must not cost a reconnect");
        subscription.Run!.Ended.IsCompleted.ShouldBeFalse("the run announced to the caller must still be the live one");

        await subscription.Unsubscribe(_ => { }, ct);
    }

    /// <summary>
    /// A throwing OnDropped must not unwind the retry loop it's reported from (see ReportDrop) — the
    /// replacement run still has to come up.
    /// </summary>
    [Test]
    public async Task A_throwing_OnDropped_does_not_stop_the_retry_loop(CancellationToken ct) {
        var subscription = new FakeSubscription(retryDelay: TimeSpan.FromMilliseconds(20));

        await subscription.Subscribe(_ => { }, (_, _, _) => throw new InvalidOperationException("boom from OnDropped"), ct);

        subscription.Run!.Fail(DropReason.SubscriptionError, new InvalidOperationException("connection lost"));

        (await Wait.Until(() => subscription.Connects >= 2, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("a throwing OnDropped must not prevent the replacement run from connecting");

        await subscription.Unsubscribe(_ => { }, ct);
    }

    /// <summary>
    /// OnUnsubscribed answers "is this subscription stopped", not "did this call stop it" — so it must fire
    /// even for a subscription that never started.
    /// </summary>
    [Test]
    [Timeout(10_000)]
    public async Task Unsubscribe_reports_even_when_the_subscription_never_started(CancellationToken ct) {
        var subscription = new FakeSubscription();

        var called = false;
        await subscription.Unsubscribe(_ => called = true, ct);

        called.ShouldBeTrue("a caller that asked to be told the subscription stopped is owed an answer either way");
    }

    /// <summary>
    /// A first connect that gave up already retired its session, so Unsubscribe has nothing left to stop —
    /// it must still report, and return promptly.
    /// </summary>
    [Test]
    [Timeout(10_000)]
    public async Task Unsubscribe_reports_when_the_first_connect_gave_up(CancellationToken ct) {
        var subscription = new FakeSubscription { FailConnect = _ => new InvalidOperationException("no broker") };

        await Should.ThrowAsync<InvalidOperationException>(() => subscription.Subscribe(_ => { }, (_, _, _) => { }, ct).AsTask());

        var called = false;
        await subscription.Unsubscribe(_ => called = true, ct);

        called.ShouldBeTrue("the subscription is stopped, which is what the callback reports");
        subscription.IsRunning.ShouldBeFalse();
    }

    /// <summary>
    /// A stop that outlasts the caller's token is logged, not thrown — this is <see cref="IHostedService.StopAsync"/>
    /// on the host's shutdown token, where a throw would abort every service queued behind it. The session is
    /// discarded either way, so the warning is the only signal that teardown was still running.
    /// </summary>
    [Test]
    [Timeout(10_000)]
    public async Task An_Unsubscribe_that_outlasts_its_token_still_reports_and_does_not_throw(CancellationToken ct) {
        var logs = new CapturingLoggerFactory();

        // Longer than the caller's patience below; observes its token so teardown still finishes.
        var subscription = new FakeSubscription(loggerFactory: logs) { OnDisconnectAsync = token => Task.Delay(TimeSpan.FromSeconds(1), token) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        using var impatient = CancellationTokenSource.CreateLinkedTokenSource(ct);
        impatient.CancelAfter(TimeSpan.FromMilliseconds(100));

        var called = false;
        await subscription.Unsubscribe(_ => called = true, impatient.Token);

        called.ShouldBeTrue("a best-effort stop still reports, so a caller isn't left waiting on a teardown it can't see");

        (await logs.WaitForWarning("Gave up waiting for the subscription to stop", TimeSpan.FromSeconds(1)))
            .ShouldBeTrue("giving up on a stop in progress is worth a line of its own, since nothing throws to say it");

        // A teardown that outlived the caller must not leave the subscription refusing to start again.
        subscription.IsRunning.ShouldBeFalse("a stop that timed out still gives up ownership of the session");
        await Should.NotThrowAsync(() => subscription.Subscribe(_ => { }, (_, _, _) => { }, ct).AsTask());
        await subscription.Unsubscribe(_ => { }, ct);
    }

    /// <summary>
    /// Unsubscribe is idempotent (see TransportTeardownTests for the transport side): a repeated call must
    /// still report, without hanging or throwing.
    /// </summary>
    [Test]
    [Timeout(10_000)]
    public async Task A_repeated_Unsubscribe_reports_again_and_does_not_throw(CancellationToken ct) {
        var subscription = new FakeSubscription();

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        var calls = 0;
        await subscription.Unsubscribe(_ => Interlocked.Increment(ref calls), ct);
        await subscription.Unsubscribe(_ => Interlocked.Increment(ref calls), ct);

        calls.ShouldBe(2, "both callers asked to be told the subscription stopped, and for both of them it is");
    }

    /// <summary>
    /// A clean stop reports no drop, even when the transport fails the run on its own cancellation — that
    /// Fail runs inside CancelAsync and wins the first-wins race, so without a lifetime check a clean stop
    /// would be reported as an unhealthy drop.
    /// </summary>
    [Test]
    [Timeout(10_000)]
    public async Task A_clean_stop_reports_no_drop_when_the_transport_fails_the_run_on_the_way_out(CancellationToken ct) {
        var subscription = new FakeSubscription { FailRunOnCancellation = true };

        var drops = 0;
        await subscription.Subscribe(_ => { }, (_, _, _) => Interlocked.Increment(ref drops), ct);
        await subscription.Unsubscribe(_ => { }, ct);

        drops.ShouldBe(0, "a drop raised by our own cancellation describes the shutdown, not a failure to report");
    }

    /// <summary>
    /// A Disconnect that throws on every run — a broker already unreachable — must cost a log line, not the
    /// retry loop, or the subscription that most needs to reconnect is the one that silently stops trying.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task A_Disconnect_that_throws_does_not_stop_the_subscription_retrying(CancellationToken ct) {
        var subscription = new FakeSubscription(retryDelay: TimeSpan.FromMilliseconds(20)) {
            Pump         = _ => Task.FromException(new InvalidOperationException("connection lost")),
            OnDisconnect = _ => throw new InvalidOperationException("cannot let go")
        };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        (await Wait.Until(() => subscription.Connects >= 3, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("a failed release ends the run, not the subscription");

        await subscription.Unsubscribe(_ => { }, ct);
    }

    /// <summary>
    /// Disposal must stop the subscription before releasing the pipe, or a running supervisor keeps
    /// reconnecting and dispatching into disposed filters.
    /// </summary>
    [Test]
    [Timeout(10_000)]
    public async Task Disposing_a_running_subscription_stops_it_before_releasing_the_pipe(CancellationToken ct) {
        var filter      = new CountingDisposableFilter();
        var disconnects = 0;

        var subscription = new FakeSubscription(pipe: new ConsumePipe().AddFilterFirst(filter).AddDefaultConsumer(new NoOpHandler())) {
            OnDisconnect = _ => Interlocked.Increment(ref disconnects)
        };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);
        subscription.IsRunning.ShouldBeTrue();

        await subscription.DisposeAsync();

        subscription.IsRunning.ShouldBeFalse("a disposed subscription that keeps its supervisor is one that keeps reconnecting into a disposed pipe");
        disconnects.ShouldBe(1, "the transport is released first, which is the whole point of stopping before disposing");
        filter.Disposals.ShouldBe(1);

        await subscription.DisposeAsync();
        filter.Disposals.ShouldBe(1, "disposal is once, however many times it is asked for");
    }

    /// <summary>
    /// Concurrent disposals must release the pipe once — a check-then-set guard letting both through would
    /// dispose every filter in it twice.
    /// </summary>
    [Test]
    [Timeout(10_000)]
    public async Task Concurrent_disposals_release_the_pipe_once(CancellationToken ct) {
        var filter       = new CountingDisposableFilter();
        var subscription = new FakeSubscription(pipe: new ConsumePipe().AddFilterFirst(filter).AddDefaultConsumer(new NoOpHandler()));

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () => await subscription.DisposeAsync(), ct)));

        filter.Disposals.ShouldBe(1, "two disposals reaching the pipe means every filter in it is disposed twice");
    }

    /// <summary>
    /// Disconnect must get a token of its own, not the run token teardown just cancelled — an already-cancelled
    /// token turns release logic into an instant no-op and leaks the connection.
    /// </summary>
    [Test]
    public async Task Disconnect_receives_a_token_that_is_not_cancelled(CancellationToken ct) {
        bool? teardownTokenCancelled = null;
        bool? runTokenCancelledByThen = null;
        SubscriptionRun? capturedRun = null;

        var subscription = new FakeSubscription {
            OnDisconnect = token => {
                teardownTokenCancelled  = token.IsCancellationRequested;
                runTokenCancelledByThen = capturedRun?.Token.IsCancellationRequested;
            }
        };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);
        capturedRun = subscription.Run;

        await subscription.Unsubscribe(_ => { }, ct);

        runTokenCancelledByThen.ShouldBe(true, "the run token must already be cancelled by the time Disconnect runs");
        teardownTokenCancelled.ShouldBe(false, "Disconnect must get its own budget, not the run token that was just cancelled");
    }

    /// <summary>
    /// A drop landing right before shutdown must not turn Unsubscribe into a wait for the retry delay to elapse,
    /// nor spend another connect attempt.
    /// </summary>
    [Test]
    public async Task Cancelling_during_the_retry_delay_ends_the_subscription_without_another_connect(CancellationToken ct) {
        var transitions  = new Transitions();
        var subscription = new FakeSubscription(retryDelay: TimeSpan.FromSeconds(10));

        await subscription.Subscribe(_ => transitions.Subscribed(), (_, _, _) => transitions.Dropped(), ct);

        subscription.Run!.Fail(DropReason.SubscriptionError, new InvalidOperationException("connection lost"));

        (await Wait.Until(() => transitions.Drops >= 1, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the drop should have been reported before the retry delay starts");

        var stopwatch = Stopwatch.StartNew();
        await subscription.Unsubscribe(_ => { }, ct);
        stopwatch.Stop();

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5), "Unsubscribe must not wait out the retry delay it interrupted");
        subscription.Connects.ShouldBe(1, "cancelling during the retry delay must not spend another connect attempt");
    }

    /// <summary>
    /// A caller told its first connect failed must be able to retry immediately — if the session were still
    /// published during teardown, the retry would hit "already running" and be turned away by a dying session.
    /// </summary>
    /// <remarks>
    /// The slow Disconnect observes its token so this fails on a bad ordering instead of hanging.
    /// </remarks>
    [Test]
    public async Task A_retry_after_a_failed_first_connect_actually_starts(CancellationToken ct) {
        var subscription = new FakeSubscription(retryDelay: TimeSpan.FromMilliseconds(50)) {
            FailConnect       = attempt => attempt == 0 ? new InvalidOperationException("no broker") : null,
            OnDisconnectAsync = token => Task.Delay(300, token)
        };

        await Should.ThrowAsync<InvalidOperationException>(() => subscription.Subscribe(_ => { }, (_, _, _) => { }, ct).AsTask());

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        subscription.Connects.ShouldBe(2, "the retry must start a run of its own rather than returning against the dying session");
        subscription.IsRunning.ShouldBeTrue("the retry must leave the subscription up");

        await subscription.Unsubscribe(_ => { }, ct);
    }

    /// <summary>
    /// One run per instance: a second Subscribe must be refused before its callbacks are wired in, or the
    /// live run starts reporting to a caller that was told nothing.
    /// </summary>
    [Test]
    public async Task A_second_subscribe_while_running_is_refused_and_keeps_the_live_callbacks(CancellationToken ct) {
        var firstDrops  = new ConcurrentQueue<DropReason>();
        var secondDrops = new ConcurrentQueue<DropReason>();

        // Parked until the refusal has landed.
        var release      = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = new FakeSubscription(retryDelay: TimeSpan.FromMilliseconds(20)) { Pump = _ => release.Task };

        await subscription.Subscribe(_ => { }, (_, reason, _) => firstDrops.Enqueue(reason), ct);

        // Check the message, not just the type — a connect failure is an InvalidOperationException too.
        var refused = await Should.ThrowAsync<InvalidOperationException>(
            () => subscription.Subscribe(_ => { }, (_, reason, _) => secondDrops.Enqueue(reason), ct).AsTask()
        );

        refused.Message.ShouldContain("already running");

        subscription.IsRunning.ShouldBeTrue("a refused caller must leave the running subscription alone");

        release.TrySetResult();

        (await Wait.Until(() => !firstDrops.IsEmpty, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the drop belongs to the caller that started the run");

        await subscription.Unsubscribe(_ => { }, ct);

        secondDrops.ShouldBeEmpty("a caller that was refused must not be wired into the live run");
    }

    /// <summary>
    /// Whatever a transport hands <see cref="SubscriptionRun.Fail"/> is what the caller hears: same reason,
    /// same exception instance, once per run. The supervisor neither classifies nor substitutes.
    /// </summary>
    /// <remarks>
    /// The cancellation case matters most: a cancelled task has no <see cref="Task.Exception"/>, so reading
    /// only that field would lose it to a synthetic "processing ended".
    /// </remarks>
    [Test]
    [Timeout(10_000)]
    [MethodDataSource(nameof(Failures))]
    public async Task A_failed_run_reports_its_reason_and_exception_verbatim(DropReason reason, Exception failure, CancellationToken ct) {
        var drops = new ConcurrentQueue<(DropReason Reason, Exception? Exception)>();

        // Long enough that the failed run is the only one, so the queue holds exactly what it reported.
        var subscription = new FakeSubscription(retryDelay: TimeSpan.FromSeconds(30));

        await subscription.Subscribe(_ => { }, (_, r, e) => drops.Enqueue((r, e)), ct);

        subscription.Run!.Fail(reason, failure);

        (await Wait.Until(() => !drops.IsEmpty, TimeSpan.FromSeconds(5))).ShouldBeTrue("a failed run is a drop to report");

        await subscription.Unsubscribe(_ => { }, ct);

        drops.Count.ShouldBe(1, "one failure is one drop — health flapping is what a double report looks like");

        var reported = drops.Single();
        reported.Reason.ShouldBe(reason);
        reported.Exception.ShouldBeSameAs(failure, "the cause must arrive as it was raised, not re-wrapped or replaced");
    }

    public static IEnumerable<Func<(DropReason, Exception)>> Failures() {
        yield return () => (DropReason.ServerError, new InvalidOperationException("the connection went away"));
        yield return () => (DropReason.SubscriptionError, new FormatException("the pump broke"));
        yield return () => (DropReason.ServerError, new OperationCanceledException("the pump hit its own deadline"));
    }

    /// <summary>
    /// The retry delay has to actually elapse between attempts — a dead connection spinning into hundreds of
    /// reconnects a second would still pass a test that only asserts a resubscribe happens.
    /// </summary>
    [Test]
    public async Task The_retry_delay_elapses_between_connect_attempts(CancellationToken ct) {
        var delay = TimeSpan.FromMilliseconds(300);

        // Every attempt after the first fails, so every gap between connects is a retry delay.
        var subscription = new FakeSubscription(retryDelay: delay) { FailConnect = attempt => attempt == 0 ? null : new InvalidOperationException("still down") };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        subscription.Run!.Fail(DropReason.SubscriptionError, new InvalidOperationException("connection lost"));

        (await Wait.Until(() => subscription.ConnectTicks.Count >= 4, TimeSpan.FromSeconds(10)))
            .ShouldBeTrue($"expected the loop to keep retrying, only saw {subscription.ConnectTicks.Count} connects");

        await subscription.Unsubscribe(_ => { }, ct);

        var ticks = subscription.ConnectTicks.ToArray();

        // Margin under the configured delay since the timer may fire a little early.
        var floor = delay - TimeSpan.FromMilliseconds(50);

        for (var i = 1; i < ticks.Length; i++) {
            Stopwatch.GetElapsedTime(ticks[i - 1], ticks[i])
                .ShouldBeGreaterThan(floor, $"connect {i} followed connect {i - 1} without waiting out the retry delay");
        }
    }

    /// <summary>
    /// The previous run's pump must be joined before the next connect, or two runs end up delivering into the
    /// same consume pipe at once.
    /// </summary>
    [Test]
    public async Task A_pump_that_lingers_after_cancellation_is_joined_before_the_next_connect(CancellationToken ct) {
        var subscription = new FakeSubscription(retryDelay: TimeSpan.FromMilliseconds(20)) {
            Pump = async run => {
                try { await Task.Delay(Timeout.Infinite, run.Token); } catch (OperationCanceledException) { }

                // The overrun: keeps running well past cancellation, like a pump parked in a handler or a slow read.
                await Task.Delay(500, CancellationToken.None);
            }
        };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        subscription.Run!.Fail(DropReason.SubscriptionError, new InvalidOperationException("connection lost"));

        (await Wait.Until(() => subscription.Connects >= 2, TimeSpan.FromSeconds(10)))
            .ShouldBeTrue("the replacement run should have connected");

        await subscription.Unsubscribe(_ => { }, ct);

        subscription.MaxLivePumpsAtConnect.ShouldBe(0, "a run started while its predecessor's pump was still running");
    }



    record TestOptions : SubscriptionOptions;

    /// <summary>
    /// A minimal transport whose only behaviour is what a test configures: which connects fail, the retry
    /// delay, and what Disconnect is handed.
    /// </summary>
    sealed class FakeSubscription(TimeSpan? retryDelay = null, ILoggerFactory? loggerFactory = null, ConsumePipe? pipe = null)
        : EventSubscription<TestOptions>(
            new() { SubscriptionId = $"supervisor-{Guid.NewGuid()}", RetryDelay = retryDelay ?? TimeSpan.FromMilliseconds(20) },
            pipe ?? new ConsumePipe().AddDefaultConsumer(new NoOpHandler()),
            loggerFactory,
            null
        ) {
        int              _connects;
        int              _livePumps;
        int              _maxLivePumpsAtConnect;
        SubscriptionRun? _run;

        public int Connects => Volatile.Read(ref _connects);

        /// <summary>
        /// Pumps seen running at the moment a connect started. Above zero means a run began alongside its
        /// predecessor's pump, which the bounded join exists to prevent.
        /// </summary>
        public int MaxLivePumpsAtConnect => Volatile.Read(ref _maxLivePumpsAtConnect);

        /// <summary>
        /// When each connect started, so a test can assert on the gaps between them.
        /// </summary>
        public ConcurrentQueue<long> ConnectTicks { get; } = new();

        /// <summary>
        /// The run from the most recent Connect, kept past its own teardown since some tests fail it after the fact.
        /// </summary>
        public SubscriptionRun? Run => Volatile.Read(ref _run);

        public TimeSpan RetryDelayForTests => Options.RetryDelay;

        /// <summary>
        /// Consulted with the zero-based attempt number before each connect; a returned exception fails that attempt.
        /// </summary>
        public Func<int, Exception?>? FailConnect { get; init; }

        public Action<CancellationToken>? OnDisconnect { get; init; }

        /// <summary>
        /// Held for as long as this returns, so a test can widen the teardown window a caller races.
        /// </summary>
        public Func<CancellationToken, Task>? OnDisconnectAsync { get; init; }

        /// <summary>
        /// Started on the run when set; counted while it runs so the join can be asserted on.
        /// </summary>
        public Func<SubscriptionRun, Task>? Pump { get; init; }

        /// <summary>
        /// Fails the run from a registration on its own token, simulating a client that reports a drop the
        /// instant the run token is cancelled, unaware the shutdown is ours.
        /// </summary>
        public bool FailRunOnCancellation { get; init; }

        protected override ValueTask Connect(SubscriptionRun run) {
            var attempt = Interlocked.Increment(ref _connects) - 1;
            Volatile.Write(ref _run, run);
            ConnectTicks.Enqueue(Stopwatch.GetTimestamp());

            // Assigned below, but the callback awaiting it is registered here, unconditionally, so teardown
            // reaches this fake's release even when Connect is about to fail outright.
            Task? pumping = null;

            run.OnDisconnect(async ct => {
                OnDisconnect?.Invoke(ct);

                if (OnDisconnectAsync is not null) await OnDisconnectAsync(ct).ConfigureAwait(false);

                // Joining last keeps MaxLivePumpsAtConnect honest: the next Connect must never see this one still running.
                if (pumping is not null) await pumping.ConfigureAwait(false);
            });

            if (FailRunOnCancellation) run.Token.Register(() => run.Fail(DropReason.ServerError, new("connection closed")));

            var live = Volatile.Read(ref _livePumps);

            // Racy by design: any reading above zero is a predecessor still pumping.
            if (live > MaxLivePumpsAtConnect) Volatile.Write(ref _maxLivePumpsAtConnect, live);

            if (FailConnect?.Invoke(attempt) is { } failure) throw failure;

            // Started on a task of its own so it never runs inline on the supervisor's stack during Connect.
            if (Pump is { } pump) pumping = Task.Run(() => Counted(pump, run), CancellationToken.None);

            return default;
        }

        /// <summary>
        /// Runs the configured pump and reports its own death, the same contract a real transport keeps.
        /// </summary>
        async Task Counted(Func<SubscriptionRun, Task> pump, SubscriptionRun run) {
            Interlocked.Increment(ref _livePumps);

            try {
                await TransportPump.Run(run, () => pump(run), "Processing ended while the connection was up").ConfigureAwait(false);
            } finally { Interlocked.Decrement(ref _livePumps); }
        }
    }

    sealed class NoOpHandler : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => new(EventHandlingStatus.Success);
    }

    /// <summary>
    /// A pass-through filter that counts how many times the pipe disposed it.
    /// </summary>
    sealed class CountingDisposableFilter : ConsumeFilter<IMessageConsumeContext>, IAsyncDisposable {
        int _disposals;

        public int Disposals => Volatile.Read(ref _disposals);

        protected override ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next)
            => next?.Value.Send(context, next.Next) ?? default;

        public ValueTask DisposeAsync() {
            Interlocked.Increment(ref _disposals);

            return default;
        }
    }
}
